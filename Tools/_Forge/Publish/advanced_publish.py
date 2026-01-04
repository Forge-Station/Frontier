"""
Продвинутый паблиш с параллельной загрузкой и аргументами
Github: FireFoxPhoenix
"""

#!/usr/bin/env python3

#!/usr/bin/env python3

import argparse
import requests
import os
import subprocess
import threading
import logging
import sys
from typing import Iterable
from concurrent.futures import ThreadPoolExecutor, as_completed

thread_session = threading.local()
logger = logging.getLogger(__name__)

#
# CONFIGURATION PARAMETERS
# Forks should change these to publish to their own infrastructure.
#
ROBUST_CDN_URL = "https://cdn.corvaxforge.ru/"

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fork-id", required=True)
    parser.add_argument("--publish-token")
    parser.add_argument("--max-workers", type=int, default=4)
    parser.add_argument("--release_dir", default="release")

    args = parser.parse_args()
    fork_id = args.fork_id
    publish_token = args.publish_token
    max_workers = args.max_workers
    release_dir = args.release_dir
    
    if fork_id == "" or fork_id == None:
        logger.critical("Fork id was not entered")
        raise KeyError()
    
    if publish_token not in os.environ:
        logger.critical("Publish token not found")
        sys.exit(1)
    publish_token = os.environ[publish_token]
    if not publish_token:
        logger.critical(f"Publish token is empty")
        sys.exit(1)   
    
    #if "GITHUB_SHA" not in os.environ: # TODO: сделать через argument
    #    logger.critical("GITHUB_SHA environment variable not set")
    #    sys.exit(1)
    version = os.environ["GITHUB_SHA"]
    logger.info(f"Starting publish on Robust.Cdn for version {version}")

    session = create_session(publish_token, max_workers=max_workers)
    data = {
        "version": version,
        "engineVersion": get_engine_version(),
    }
    headers = {
        "Content-Type": "application/json"
    }
    resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/start", json=data, headers=headers)
    resp.raise_for_status()
    logger.info("Publish successfully started, adding files...")

    files = list(get_files_to_publish(release_dir))
    if not files:
        logger.warning("No files found to publish")
        
    logger.info(f"Uploading {len(files)} files using {max_workers} parallel workers...")
    successful = 0
    failed = 0
    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        future_files = {
            executor.submit(upload_file, str(file), fork_id, publish_token, version, max_workers): file for file in files
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
        logger.warning(f"Upload completed with {failed} failures")
        # sys.exit(1)
    else:
        logger.info(f"All {successful} files uploaded successfully")
    
    logger.info("Finishing publish...")
    data = {
        "version": version
    }
    headers = {
        "Content-Type": "application/json"
    }
    resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/finish", json=data, headers=headers)
    resp.raise_for_status()

    logger.info("Publish completed")


def get_files_to_publish(release_dir: str) -> Iterable[str]:
    try:
        with os.scandir(release_dir) as d:
            for entry in d:
                if entry.is_file():
                    yield entry.path
    except FileNotFoundError:
        logger.error(f"Release directory '{release_dir}' not found")
        return []
    except PermissionError:
        logger.error(f"No permission to read directory '{release_dir}'")
        return []


def get_engine_version() -> str:
    try:
        proc = subprocess.run(["git", "describe","--tags", "--abbrev=0"], stdout=subprocess.PIPE, cwd="RobustToolbox", check=True, encoding="UTF-8")
        tag = proc.stdout.strip()
        if not tag.startswith("v"):
            logger.warning(f"Unexpected tag format: {tag}")
            return tag
        return tag[1:]
    except subprocess.CalledProcessError as e:
        stderr = (e.stderr or "").strip()
        logger.error(f"Failed to get engine version: {stderr}")
        return "unknown"
    except FileNotFoundError:
        logger.error("RobustToolbox directory not found")
        return "unknown"

def upload_file(file_path: str, fork_id: str, publish_token: str, version: str, max_workers: int):
    if not hasattr(thread_session, "session"):
        thread_session.session = create_session(publish_token, max_workers)
    session = thread_session.session
    with open(file_path, "rb") as file:
        headers = {
            "Content-Type": "application/octet-stream",
            "Robust-Cdn-Publish-File": os.path.basename(file_path),
            "Robust-Cdn-Publish-Version": version
        }
        resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/file", data=file, headers=headers)
        resp.raise_for_status()
    return file_path

def create_session(publish_token: str, max_workers: int) -> requests.Session:
    session = requests.Session()
    adapter = requests.adapters.HTTPAdapter(
        pool_connections=max(10, max_workers * 2),
        pool_maxsize=max(10, max_workers * 2),
        max_retries=3
    )
    session.mount("https://", adapter)
    session.mount("http://", adapter)
    session.headers = {
        "Authorization": f"Bearer {publish_token}",
    }
    return session

if __name__ == '__main__':
    main()
