"""Copy completed training files before loading; export both roles in one process."""

import argparse
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sys
import uuid


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def receipt_checkpoint(runs, value):
    """Use the relocated run first, while retaining immutable original receipts."""
    if not value:
        return None
    normalized = str(value).replace("\\", "/")
    match = re.search(r"(?:^|/)rl/runs/(.+)$", normalized, re.IGNORECASE)
    if match:
        relative = Path(match.group(1))
        if ".." not in relative.parts:
            relocated = runs / relative
            if relocated.is_file():
                return relocated
    original = Path(value)
    return original if original.is_file() else None


def resolve_source(runs, run, profile, role, mode):
    if mode == "latest":
        for path in (runs / run / profile / role / "latest.pt",
                     runs / f"{run}-{role}" / "latest.pt"):
            if path.is_file():
                return path, None
        raise FileNotFoundError(f"No latest {role} checkpoint in {run}/{profile}")
    for directory in (runs / run / "exports" / profile, runs / run):
        choices = [(int(match.group(1)), path) for path in directory.glob(f"{role}-r*.json")
                   if (match := re.fullmatch(rf"{role}-r(\d+)\.json", path.name))]
        if choices:
            round_number, policy = max(choices)
            receipt = runs / run / "completed" / f"{profile}-{role}-r{round_number:04d}.json"
            metadata = json.loads(receipt.read_text(encoding="utf-8")) if receipt.is_file() else {}
            checkpoint = receipt_checkpoint(runs, metadata.get("checkpoint"))
            if checkpoint is not None:
                return checkpoint, metadata
            return policy, metadata
    raise FileNotFoundError(f"No completed {role} policy in {run}/{profile}")


def main():
    script_directory = Path(__file__).resolve().parent
    workspace = (script_directory.parent.parent
                 if (script_directory.parent / "project.godot").is_file()
                 else script_directory.parent)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--server-repo", type=Path, default=os.environ.get("PROJECT_W_SERVER_ROOT"))
    parser.add_argument("--ai-repo", type=Path,
                        default=os.environ.get("PROJECT_W_AI_ROOT", workspace / "project-w-ai"))
    parser.add_argument("--runs-root", type=Path, default=os.environ.get("PROJECT_W_RUNS_ROOT"))
    parser.add_argument("--run-directory", required=True, type=Path)
    parser.add_argument("--run", default="league5")
    parser.add_argument("--profile", default="mixed")
    parser.add_argument("--mode", choices=("latest", "completed"), default="latest")
    args = parser.parse_args()
    for value in (args.run, args.profile):
        if not re.fullmatch(r"[A-Za-z0-9_-]+", value):
            raise ValueError("Run and profile must be simple names.")
    ai = args.ai_repo.resolve()
    server = (args.server_repo or ai.parent / "project-w-server").resolve()
    runs = (args.runs_root or ai / "rl/runs").resolve()
    run_directory = args.run_directory.resolve()
    sources = {role: resolve_source(runs, args.run, args.profile, role, args.mode)
               for role in ("hero", "commander")}
    created = datetime.now(timezone(timedelta(hours=9)))
    identifier = f"{args.run}-{created:%Y%m%d-%H%M%S}-{uuid.uuid4().hex[:8]}"
    parent = run_directory / "Server/policies/snapshots"
    temporary, final = parent / f".creating-{identifier}", parent / identifier
    temporary.mkdir(parents=True, exist_ok=False)
    manifest = {"version": 1, "status": "ready", "run": args.run, "profile": args.profile,
                "mode": args.mode, "created_at": created.isoformat(), "server_repo": str(server),
                "ai_repo": str(ai), "runs_root": str(runs),
                "run_directory": str(run_directory), "policy_directory": str(final), "roles": {}}

    # Trainers atomically replace latest.pt. Copy each complete file before loading
    # so a new training save cannot change this spectator's policy halfway through.
    for role, (source, receipt) in sources.items():
        frozen = temporary / f"{role}{source.suffix}"
        shutil.copyfile(source, frozen)
        manifest["roles"][role] = {"source": str(source.resolve()), "source_sha256": digest(frozen),
                                    "captured_at": datetime.now(timezone(timedelta(hours=9))).isoformat()}
    if any(source.suffix == ".pt" for source, _ in sources.values()):
        if not (ai / "rl/model.py").is_file():
            raise FileNotFoundError(f"AI source not found: {ai}; pass --ai-repo.")
        sys.path.insert(0, str(ai / "rl"))
        import torch
        from torch import nn
        from model import load_agent
        torch.set_num_threads(1)
    for role, (source, receipt) in sources.items():
        record = manifest["roles"][role]
        output = temporary / f"{role}.json"
        if source.suffix == ".pt":
            agent, checkpoint = load_agent(temporary / f"{role}.pt")
            policy = {"obs_names": checkpoint["obs_names"], "action_names": checkpoint["action_names"],
                      "config": checkpoint.get("config", {}), "global_step": checkpoint["global_step"],
                      "layers": [{"in": layer.in_features, "out": layer.out_features,
                                  "weight": layer.weight.detach().flatten().tolist(),
                                  "bias": layer.bias.detach().tolist()}
                                 for layer in agent.actor if isinstance(layer, nn.Linear)]}
            output.write_text(json.dumps(policy), encoding="utf-8")
            record.update(checkpoint=str(final / f"{role}.pt"), checkpoint_sha256=record["source_sha256"],
                          games=checkpoint.get("games"))
        else:
            policy = json.loads(output.read_text(encoding="utf-8"))
            record["games"] = (receipt or {}).get("games")
        configured_role = policy.get("config", {}).get("role", "commander")
        if configured_role != role:
            raise ValueError(f"Expected {role} policy, found {configured_role}: {source}")
        record.update(policy=str(final / f"{role}.json"), policy_sha256=digest(output),
                      global_step=policy.get("global_step"), observations=len(policy["obs_names"]),
                      actions=len(policy["action_names"]))
    (temporary / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    temporary.rename(final)
    # ASCII JSON also survives Windows PowerShell 5.1's legacy console codepage.
    print(json.dumps(manifest))


if __name__ == "__main__":
    main()
