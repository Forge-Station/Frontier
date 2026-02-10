#!/usr/bin/env python3

"""
Продвинутый паблиш с параллельной загрузкой, аргументами и публикацией статуса паблиша в дискорд
Github: FireFoxPhoenix
"""

import argparse
import requests
import os
import subprocess
import threading
import logging
import sys
from discord_webhook import DiscordWebhook, DiscordEmbed
from typing import Iterable
from concurrent.futures import ThreadPoolExecutor, as_completed
from urllib3.util.retry import Retry

thread_session = threading.local()
logger = logging.getLogger(__name__)

#
# CONFIGURATION PARAMETERS
# Forks should change these to publish to their own infrastructure.
#
ROBUST_CDN_URL = "https://cdn.corvaxforge.ru/" # добавить в аругмент

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fork-id", required=True)
    parser.add_argument("--publish-token", required=True)
    parser.add_argument("--publish-webhook", required=False, default=None)
    parser.add_argument("--max-workers", type=int, default=4)
    parser.add_argument("--pool-connections", type=int, default=3)
    parser.add_argument("--pool-maxsize", type=int, default=10)
    parser.add_argument("--max-retries", type=int, default=3)
    parser.add_argument("--release_dir", default="release")

    args = parser.parse_args()
    fork_id = args.fork_id
    publish_token = args.publish_token
    publish_webhook = args.publish_webhook
    max_workers = args.max_workers
    pool_connections = args.pool_connections
    pool_maxsize = args.pool_maxsize
    max_retries = args.max_retries
    release_dir = args.release_dir

    if publish_webhook:
        if publish_webhook.startswith("https://discord.com/api/webhooks/"):
            pass
        else:
            if publish_webhook in os.environ:
                publish_webhook = os.environ[publish_webhook]
            else:
                publish_webhook = None
                logger.warning("Publish webhook not found")
    else:
        publish_webhook = None
        logger.warning(f"Publish webhook is empty")
    
    if fork_id == "" or fork_id == None:
        message = "Fork id was not entered"
        logger.critical(message)
        send_discord_message(message, "Critical", "ffa500", fork_id, publish_webhook)
        raise KeyError()
    
    if publish_token not in os.environ:
        message = "Publish token not found"
        logger.critical(message)
        send_discord_message(message, "Critical", "ffa500", fork_id, publish_webhook)
        sys.exit(1)
    publish_token = os.environ[publish_token]
    if not publish_token:
        message = f"Publish token is empty"
        logger.critical(message)
        # send_discord_message(message, "Critical", "ffa500", fork_id, publish_webhook)
        sys.exit(1)
    
    #if "GITHUB_SHA" not in os.environ:
    #    logger.critical("GITHUB_SHA environment variable not set")
    #    sys.exit(1)
    version = os.environ["GITHUB_SHA"]
    logger.info(f"Starting publish on Robust.Cdn for version {version}")

    session = create_session(publish_token, pool_connections, pool_maxsize, max_retries)
    data = {
        "version": version,
        "engineVersion": get_engine_version(),
    }
    headers = { "Content-Type": "application/json" }
    resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/start", json=data, headers=headers)
    resp.raise_for_status()
    logger.info("Publish successfully started, adding files...")

    files = list(get_files_to_publish(release_dir))
    if not files:
        message = "No files found to publish"
        logger.warning(message)
        send_discord_message(message, "Warning", "ffff00", fork_id, publish_webhook)
        
    logger.info(f"Uploading {len(files)} files using {max_workers} parallel workers...")
    successful = 0
    failed = 0
    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        future_files = {
            executor.submit(upload_file, str(file), fork_id, publish_token, pool_connections, pool_maxsize, max_retries, version): file for file in files
        }
        for future in as_completed(future_files):
            file_path = future_files[future]
            try:
                result = future.result()
                successful += 1
                # logger.info(f"Successfully published {os.path.basename(file_path)} ({successful}/{len(files)}")
            except Exception as e:
                failed += 1
                logger.warning(f"Failed to publish {os.path.basename(file_path)}: {e}")
    if failed:
        message = f"Upload completed with {failed} failures"
        logger.warning(message)
        send_discord_message(message, "Warning", "ffff00", fork_id, publish_webhook)
        # sys.exit(1)
    else:
        message = f"All {successful} files uploaded successfully"
        logger.info(message)
        # send_discord_message(message, "Info", "03b2f8", fork_id, publish_webhook)
    
    logger.info("Finishing publish...")
    data = { "version": version }
    headers = { "Content-Type": "application/json" }
    resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/finish", json=data, headers=headers)
    resp.raise_for_status()
    message = "Publish completed"
    logger.info(message)
    send_discord_message(message, "Info", "03b2f8", fork_id, publish_webhook)

def get_files_to_publish(release_dir: str) -> Iterable[str]:
    try:
        for root, dirs, files in os.walk(release_dir):
            for file in files:
                yield os.path.join(root, file)
    except FileNotFoundError:
        logger.error(f"Release directory '{release_dir}' not found")
        return []
    except PermissionError:
        logger.error(f"No permission to read directory '{release_dir}'")
        return []

def get_engine_version() -> str:
    try:
        proc = subprocess.run(["git", "describe","--tags", "--abbrev=0"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd="RobustToolbox", check=True, encoding="UTF-8", timeout=20)
        tag = proc.stdout.strip()
        if not tag.startswith("v"):
            logger.warning(f"Unexpected tag format: {tag}")
            return tag
        return tag[1:]
    except subprocess.CalledProcessError as e:
        stderr = (e.stderr or "").strip()
        logger.error(f"Failed to get engine version: {stderr[:300]}")
        return "unknown"
    except FileNotFoundError:
        logger.error("RobustToolbox directory not found")
        return "unknown"
    except subprocess.TimeoutExpired:
        logger.error("Git command timed out")
        return "unknown"

def upload_file(file_path: str, fork_id: str, publish_token: str, pool_connections: int, pool_maxsize: int, max_retries: int, version: str):
    try:
        if not hasattr(thread_session, "session"):
            thread_session.session = create_session(publish_token, pool_connections, pool_maxsize, max_retries)
        session = thread_session.session
        with open(file_path, "rb") as file:
            headers = {
                "Content-Type": "application/octet-stream",
                "Robust-Cdn-Publish-File": os.path.basename(file_path),
                "Robust-Cdn-Publish-Version": version
            }
            resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/file", data=file, headers=headers, timeout=(15,30))
            resp.raise_for_status()
        return file_path
    except FileNotFoundError:
        logger.error(f"File '{file_path}' not found")
        raise
    except IOError as e:
        logger.error(f"IO error reading '{file_path}': {e}")
        raise
    except Exception as e:
        logger.error(f"Unexpected error with '{file_path}': {e}")
        raise

def create_session(publish_token: str, pool_connections: int, pool_maxsize: int, max_retries: int) -> requests.Session:
    session = requests.Session()
    r = Retry(
        total=max_retries,
        backoff_factor=0.5,
        status_forcelist=[429, 500, 502, 503, 504],
        allowed_methods=["HEAD", "GET", "PUT", "POST", "DELETE", "OPTIONS", "TRACE"]
    )
    adapter = requests.adapters.HTTPAdapter(
        pool_connections=pool_connections,
        pool_maxsize=pool_maxsize,
        max_retries=r
    )
    session.mount("https://", adapter)
    session.mount("http://", adapter)
    session.headers.update({ "Authorization": f"Bearer {publish_token}" })
    session.request = lambda method, url, **kwargs: requests.Session.request(
        session, method, url, timeout=(5,30), **kwargs
    )
    return session

def send_discord_message(message: str, status: str, fork_id: str = None, color: str = "00ff00", publish_webhook: str = None):
    if not publish_webhook:
        return
    if not fork_id:
        fork_id = "unknown"
    try:
        webhook = DiscordWebhook(
            url=publish_webhook,
            username="Publish Status",
            rate_limit_retry=True
        )
        embed = DiscordEmbed(
            title=f"Publish for {fork_id}",
            color=color
        )
        embed.add_embed_field(name=status, value=message)
        embed.set_timestamp()
        webhook.add_embed(embed)
        response = webhook.execute()
        if response.status_code not in [200, 204]:
            logger.warning("The Discord message was not sent")
    except Exception as e:
        logger.error(f"The Discord message was not sent: {e}")
        return

if __name__ == '__main__':
    main()
