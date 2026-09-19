"""Loads Oathfire tool secrets from Tools/.env without ever printing them."""
from pathlib import Path
import os

TOOLS_DIR = Path(__file__).resolve().parent.parent
ENV_FILE = TOOLS_DIR / ".env"


def load_env() -> None:
    if not ENV_FILE.exists():
        raise FileNotFoundError(f"Missing secrets file: {ENV_FILE}")
    for raw in ENV_FILE.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        os.environ.setdefault(key.strip(), value.strip())


def require(name: str) -> str:
    load_env()
    value = os.environ.get(name)
    if not value:
        raise RuntimeError(f"{name} is not set in {ENV_FILE}")
    return value
