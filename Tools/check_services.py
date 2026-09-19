"""Verifies each production service key works. Prints status and quota only, never secrets."""
import json
import urllib.error
import urllib.request

from oathfire_tools.env import require


def get(url: str, headers: dict) -> tuple[int, dict | str]:
    # Cloudflare-fronted APIs (Replicate) reject urllib's default user agent with error 1010.
    request = urllib.request.Request(url, headers={"User-Agent": "OathfireTools/1.0", **headers})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            body = response.read().decode("utf-8")
            try:
                return response.status, json.loads(body)
            except json.JSONDecodeError:
                return response.status, body[:200]
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8", "replace")[:200]
    except urllib.error.URLError as error:
        return 0, str(error.reason)


def main() -> None:
    status, body = get("https://api.elevenlabs.io/v1/user/subscription", {"xi-api-key": require("ELEVENLABS_API_KEY")})
    if status == 200:
        print(f"ElevenLabs OK tier={body.get('tier')} chars_used={body.get('character_count')}/{body.get('character_limit')} voice_slots={body.get('voice_limit')}")
    else:
        print(f"ElevenLabs FAIL {status}: {body}")

    status, body = get("https://external.api.recraft.ai/v1/users/me", {"Authorization": f"Bearer {require('RECRAFT_API_TOKEN')}"})
    print(f"Recraft {'OK credits=' + str(body.get('credits')) if status == 200 else f'FAIL {status}: {body}'}")

    status, body = get("https://api.replicate.com/v1/account", {"Authorization": f"Bearer {require('REPLICATE_API_TOKEN')}"})
    print(f"Replicate {'OK account=' + str(body.get('type')) if status == 200 else f'FAIL {status}: {body}'}")

    status, body = get("https://api.meshy.ai/openapi/v1/balance", {"Authorization": f"Bearer {require('MESHY_API_KEY')}"})
    print(f"Meshy {'OK balance=' + str(body.get('balance')) if status == 200 else f'FAIL {status}: {body}'}")


if __name__ == "__main__":
    main()
