#!/usr/bin/env python3

"""
Удаляет пробелы и табуляции в конце строк в файлах с выбранными расширениями и папками
Github: FireFoxPhoenix
"""

import os

# Settings
TARGET_FOLDERS = ['Prototypes', 'Maps', 'Textures']
TARGET_EXTENSIONS = ['.ftl', '.yml']
EXCLUDED_FOLDERS = ['.git', 'bin', 'Packages', 'RobustToolbox']
EXCLUDED_EXTENSIONS = ['.meta', '.png', '.jpg', '.jpeg', '.gif', '.wav', '.mp3', '.ogg', '.dll', '.exe']
MARKER_FILE = 'SpaceStation14.sln'

# taken from tools/ss14_ru/clean_duplicates.py
def find_top_level_dir(start_directory: str) -> str:
    current_dir = start_directory
    while True:
        if MARKER_FILE in os.listdir(current_dir):
            return current_dir
        parent_dir = os.path.dirname(current_dir)
        if parent_dir == current_dir:
            print(f"Не удалось найти {MARKER_FILE} начиная с {start_directory}")
            exit(-1)
        current_dir = parent_dir

def remove_trailing_whitespace(file_path: str) -> bool:
    if not os.path.isfile(file_path):
        return False
    try:
        with open(file_path, 'r', encoding='utf-8') as file:
            content = file.read()
    except Exception as e:
        print(f"Ошибка при чтении файла {file_path}: {e}")
        return False
    lines = [line.rstrip(' \t') for line in content.splitlines()]
    end = '\n'.join(lines) + '\n'
    if content == end:
        return False
    try:
        with open(file_path, 'w', encoding='utf-8') as file:
            file.write(end)
        return True
    except Exception as e:
        print(f"Ошибка при записи файла {file_path}: {e}")
        return False

def check_all_files(folder: str):
    for root, dirs, files in os.walk(folder):
        if any(folder in root for folder in TARGET_FOLDERS):
            if any(excluded in root for excluded in EXCLUDED_FOLDERS):
                continue
            for file in files:
                if any(file.endswith(extension) for extension in TARGET_EXTENSIONS):
                    if any(file.endswith(excluded) for excluded in EXCLUDED_EXTENSIONS):
                        continue
                    file_path = os.path.join(root, file)
                    if remove_trailing_whitespace(file_path):
                        file_path = os.path.relpath(file_path, start=main_folder)
                        print(f"Удалены пробелы в конце строк в файле: {file_path}")

if __name__ == "__main__":
    start_directory = os.path.dirname(os.path.abspath(__file__))
    main_folder = find_top_level_dir(start_directory)
    check_all_files(main_folder)
    input("Нажмите Enter для выхода")
