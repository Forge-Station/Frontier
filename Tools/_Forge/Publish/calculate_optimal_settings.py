"""
Считает оптимальные настройки для максимизации скорости паблиша
Github: FireFoxPhoenix
"""

#!/usr/bin/env python3

import argparse
import os
import time
import requests
import statistics
from pathlib import Path
import socket
import json

def measure_network_speed(url: str) -> float:
    try:
        test_file = os.urandom(1024 * 1024)
        start = time.time()
        response = requests.post(f"{url}fork/test/publish/file", data=test_file, headers={"Content-Type": "application/octet-stream"}, timeout=5)
        elapsed = time.time() - start
        if response.status_code < 500:
            speed_mbps = (1 * 8) / elapsed
            return speed_mbps
    except:
        pass
    return 100.0

def measure_server_latency(url: str) -> float:
    try:
        times = []
        for _ in range(3):
            start = time.perf_counter()
            requests.get(f"{url}fork/test/publish/start", timeout=3)
            elapsed = (time.perf_counter() - start) * 1000
            times.append(elapsed)
        return statistics.median(times)
    except:
        return 100.0

def analyze_files(files_dir: str):
    total_size = 0
    file_count = 0
    sizes = [] 
    for root, dirs, files in os.walk(files_dir):
        for file in files:
            filepath = os.path.join(root, file)
            try:
                size = os.path.getsize(filepath)
                total_size += size
                sizes.append(size)
                file_count += 1
            except:
                continue
    if file_count == 0:
        return 0, 0.0, 0.0, []   
    avg_size = total_size / file_count / (1024 * 1024)
    median_size = statistics.median(sizes) / (1024 * 1024) if sizes else 0
    size_distribution = {
        'tiny': sum(1 for s in sizes if s < 100 * 1024),
        'small': sum(1 for s in sizes if 100 * 1024 <= s < 1 * 1024 * 1024),
        'medium': sum(1 for s in sizes if 1 * 1024 * 1024 <= s < 10 * 1024 * 1024),
        'large': sum(1 for s in sizes if 10 * 1024 * 1024 <= s < 100 * 1024 * 1024),
        'huge': sum(1 for s in sizes if s >= 100 * 1024 * 1024)
    }
    return file_count, avg_size, median_size, size_distribution

def calculate_optimal_settings(file_count, avg_size_mb, network_speed_mbps, latency_ms):
    base_threads = min(file_count, 16)
    if latency_ms > 200:
        network_factor = 0.5
    elif latency_ms > 100:
        network_factor = 0.7
    elif latency_ms > 50:
        network_factor = 0.9
    else:
        network_factor = 1.0
    
    if avg_size_mb < 0.1:
        size_factor = 2.0
        optimal_threads = min(base_threads, 16)
    elif avg_size_mb < 1:
        size_factor = 1.5
        optimal_threads = min(base_threads, 12)
    elif avg_size_mb < 10:
        size_factor = 1.0
        optimal_threads = min(base_threads, 8)
    elif avg_size_mb < 50:
        size_factor = 0.7
        optimal_threads = min(base_threads, 4)
    else:
        size_factor = 0.5
        optimal_threads = min(base_threads, 2)
    
    bandwidth_per_thread = network_speed_mbps / optimal_threads
    if bandwidth_per_thread < 1:
        optimal_threads = max(1, int(network_speed_mbps))
    
    adjusted_threads = int(optimal_threads * network_factor * size_factor)
    adjusted_threads = max(1, min(adjusted_threads, file_count, 16))
    
    pool_connections = 3
    
    if adjusted_threads <= 2:
        pool_maxsize = 4
    elif adjusted_threads <= 4:
        pool_maxsize = 8
    elif adjusted_threads <= 8:
        pool_maxsize = 12
    else:
        pool_maxsize = 16
    
    total_size_mb = file_count * avg_size_mb
    upload_time_single = (total_size_mb * 8) / network_speed_mbps
    estimated_time = upload_time_single / adjusted_threads
    estimated_time += (latency_ms / 1000) * file_count / adjusted_threads
    
    return {
        'max_workers': adjusted_threads,
        'pool_connections': pool_connections,
        'pool_maxsize': pool_maxsize,
        'estimated_time_minutes': estimated_time / 60,
        'speedup': upload_time_single / estimated_time if estimated_time > 0 else 1,
        'bandwidth_per_thread_mbps': network_speed_mbps / adjusted_threads
    }

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--files-dir", default="release")
    parser.add_argument("--server-url", required=True)
    parser.add_argument("--network-speed", type=float)
    parser.add_argument("--skip-measure", action="store_true")
    
    args = parser.parse_args()
    
    print("Analyzing files...")
    file_count, avg_size, median_size, size_dist = analyze_files(args.files_dir)
    
    if file_count == 0:
        print("No files found.")
        return
    
    if args.skip_measure:
        latency = 100.0
        network_speed = args.network_speed or 100.0
        print("Using default measurements (skipped)")
    else:
        print("Measuring server latency...")
        latency = measure_server_latency(args.server_url)
        
        if args.network_speed:
            network_speed = args.network_speed
            print(f"Using provided network speed: {network_speed} Mbps")
        else:
            print("Measuring network speed...")
            network_speed = measure_network_speed(args.server_url)
    
    print("\n" + "="*10)
    print(f"Total files: {file_count}")
    print(f"Total size: {file_count * avg_size:.1f} MB")
    print(f"Average size: {avg_size:.2f} MB")
    print(f"Median size: {median_size:.2f} MB")
    print(f"Size distribution:")
    print(f"  <100KB: {size_dist['tiny']} files")
    print(f"  100KB-1MB: {size_dist['small']} files")
    print(f"  1-10MB: {size_dist['medium']} files")
    print(f"  10-100MB: {size_dist['large']} files")
    print(f"  >100MB: {size_dist['huge']} files")
    
    print("\n" + "="*10)
    print(f"Network speed: {network_speed:.1f} Mbps")
    print(f"Server latency: {latency:.1f} ms")
    
    print("\n" + "="*10)
    print("OPTIMAL SETTINGS")
    print("="*10)
    
    optimal = calculate_optimal_settings(file_count, avg_size, network_speed, latency)
    
    print(f"Recommended --max-workers: {optimal['max_workers']}")
    print(f"Recommended --pool-connections: {optimal['pool_connections']}")
    print(f"Recommended --pool-maxsize: {optimal['pool_maxsize']}")
    print(f"Estimated upload time: {optimal['estimated_time_minutes']:.1f} minutes")
    print(f"Speedup vs single thread: {optimal['speedup']:.1f}x")
    print(f"Bandwidth per thread: {optimal['bandwidth_per_thread_mbps']:.1f} Mbps")
    print(f"Network quality: {'Good' if latency < 50 else 'Average' if latency < 100 else 'Poor'}")
    
if __name__ == "__main__":
    main()
