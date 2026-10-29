"""Archive completed files from the UI-verified Mixamo export queue."""
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
QUEUE = ROOT / "Documentation/CharacterProduction/mixamo-export-queue.json"
MANIFEST = ROOT / "Documentation/CharacterProduction/mixamo-assets.json"


def main():
    queue = json.loads(QUEUE.read_text())
    completed = {c["id"] for c in json.loads(MANIFEST.read_text())["clips"]}
    for item in queue:
        source = Path(item["expectedDownload"])
        if item["id"] in completed:
            item["state"] = "archived"
        elif source.is_file():
            subprocess.run([sys.executable, str(ROOT / "Tools/archive_mixamo_download.py"), str(source),
                "--id", item["id"], "--source-name", item.get("sourceName", item["title"]),
                "--source-page", item["sourcePage"]], cwd=ROOT, check=True)
            item["state"] = "archived"
    QUEUE.write_text(json.dumps(queue, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"archived": [i["id"] for i in queue if i["state"] == "archived"],
        "waiting": [i["id"] for i in queue if i["state"] != "archived"]}))


if __name__ == "__main__":
    main()
