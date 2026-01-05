#!/usr/bin/env python3
"""
Обновляет цены шаттлов в прототипах на основе данных из логов
Округляет цены до ближайших сотен в большую сторону
Github: FireFoxPhoenix
"""

import os
import re
import sys
import argparse

def find_top_level_dir(start_directory: str, marker_file: str = MARKER_FILE) -> str:
    current_dir = start_directory
    while True:
        try:
            if marker_file in os.listdir(current_dir):
                return current_dir
        except (FileNotFoundError, PermissionError):
            pass
        parent_dir = os.path.dirname(current_dir)
        if parent_dir == current_dir:
            print(f"Failed to find {marker_file} starting from {start_directory}")
            sys.exit(-1)
        current_dir = parent_dir

def parse_log_file(log_path: str):
    shuttle_prices = {}
    if not os.path.exists(log_path):
        print(f"Log file not found: {log_path}")
        return shuttle_prices
    try:
        with open(log_path, 'r', encoding='utf-8') as f:
            log_content = f.read()
    except Exception as e:
        print(f"Error reading log file: {e}")
        return shuttle_prices
    pattern = r'Arbitrage possible on (\w+?)\. Minimal price should be ([\d,]+)'
    matches = re.findall(pattern, log_content)
    for shuttle_name, price_str in matches:
        price_str_clean = price_str.replace(',', '').replace('.', '')
        try:
            price = float(price_str_clean)
        except ValueError:
            print(f"Failed to parse price for {shuttle_name}: {price_str}")
            continue
        corrected_price = ((price + 99) // 100) * 100
        shuttle_prices[shuttle_name] = int(corrected_price)
    print(f"Found {len(shuttle_prices)} shuttles in log")
    return shuttle_prices

def find_shuttle_prototype(root_dir: str, shuttle_name: str, prototypes_folder: str):
    prototypes_path = os.path.join(root_dir, prototypes_folder)
    if not os.path.exists(prototypes_path):
        print(f"Prototypes folder not found: {prototypes_path}")
        return None
    for root, dirs, files in os.walk(prototypes_path):
        for file in files:
            if file.endswith('.yml'):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()
                        if f"id: {shuttle_name}" in content or f"\n  id: {shuttle_name}" in content:
                            return file_path
                except Exception as e:
                    print(f"Error reading file {file_path}: {e}")
    return None

def update_shuttle_price(file_path: str, shuttle_name: str, new_price: int, dry_run: bool = False):
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
    except Exception as e:
        print(f"Error reading file {file_path}: {e}")
        return False
    old_pattern = rf'(id:\s*{shuttle_name}\s*\n(?:[ \t-]*.*\n)*?[ \t-]*price:\s*)(\d+)'
    match = re.search(old_pattern, content, re.MULTILINE)
    if not match:
        pattern = rf'(id:\s*{shuttle_name}.*?\n.*?price:\s*)(\d+)'
        match = re.search(pattern, content, re.DOTALL)
    if match:
        old_price = match.group(2)
        new_content = content[:match.start(2)] + str(new_price) + content[match.end(2):]
        if content == new_content:
            print(f"  Price already correct: {old_price}")
            return False
        if dry_run:
            print(f"  [DRY RUN] Would update: {old_price} -> {new_price}")
            return True
        try:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"  Updated: {old_price} -> {new_price}")
            return True
        except Exception as e:
            print(f"  Error writing file: {e}")
            return False
    else:
        print(f"  Could not find price for shuttle {shuttle_name} in file")
        return False

def main():
    parser = argparse.ArgumentParser(formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        '--log', 
        required=True,
        help=f'Path to test log file'
    )
    parser.add_argument(
        '--prototypes', 
        default='Resources/Prototypes/_Forge/Shipyard',
        help=f'Shuttle prototypes folder'
    )
    parser.add_argument(
        '--marker', 
        default='SpaceStation14.sln',
        help=f'Marker file for finding project root'
    )
    parser.add_argument(
        '--dry-run', 
        action='store_true',
        help='Test mode - shows changes without applying them'
    )
    args = parser.parse_args()
    start_directory = os.path.dirname(os.path.abspath(__file__))
    root_dir = find_top_level_dir(start_directory, args.marker)
    log_path = os.path.join(root_dir, args.log)
    shuttle_prices = parse_log_file(log_path)
    if not shuttle_prices:
        print("No shuttle data found in log")
        sys.exit(0)
    print(f"\nProcessing {len(shuttle_prices)} shuttles...")
    updated_count = 0
    not_found_count = 0
    already_correct_count = 0
    for shuttle_name, new_price in shuttle_prices.items():
        print(f"\nShuttle: {shuttle_name}")
        print(f"  New price: {new_price}")
        prototype_file = find_shuttle_prototype(root_dir, shuttle_name, args.prototypes)
        if prototype_file:
            print(f"  Found file: {os.path.relpath(prototype_file, root_dir)}")
            updated = update_shuttle_price(prototype_file, shuttle_name, new_price, args.dry_run)
            if updated:
                updated_count += 1
            else:
                already_correct_count += 1
        else:
            print(f"  Prototype file not found for shuttle {shuttle_name}")
            not_found_count += 1

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\nInterrupted by user")
        sys.exit(0)
    except Exception as e:
        print(f"\nCritical error: {e}")
        sys.exit(1)
