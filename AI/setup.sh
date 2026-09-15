#!/bin/bash

set -e

echo "=== Setting up Stroke Rehab AI ==="

python3 -m venv .venv

echo "=== Activating virtual environment ==="
source .venv/bin/activate

echo "=== Installing dependencies ==="
pip install --upgrade pip
pip install -r requirements.txt

echo "=== AI setup completed ==="