#!/bin/bash

set -e
cd -- "$(dirname -- "$0")"

echo "=== Setting up Stroke Rehab AI ==="

"${PYTHON:-python3}" -m venv .venv

echo "=== Activating virtual environment ==="
source .venv/bin/activate

echo "=== Installing dependencies ==="
python -m pip install -r requirements.txt
python -m pip check
python main.py --self-test

echo "=== AI setup completed ==="
