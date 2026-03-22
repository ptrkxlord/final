from core.resolver import (Resolver, _JSON)
json = Resolver.get_mod(_JSON)

"""
core/bridge_cli.py — CLI for bridge management
Run: python -m core.bridge_cli
"""

import sys
import argparse
from core.bridge_manager import bridge_manager

def main():
    parser = argparse.ArgumentParser(description='Bridge Manager CLI')
    parser.add_argument('--list', action='store_true', help='List all bridges')
    parser.add_argument('--deploy', action='store_true', help='Deploy new bridge')
    parser.add_argument('--test', metavar='URL', help='Test a bridge URL')
    parser.add_argument('--remove', metavar='URL', help='Remove a bridge')
    parser.add_argument('--stats', action='store_true', help='Show stats')
    parser.add_argument('--switch', action='store_true', help='Switch to next bridge')

    args = parser.parse_args()

    if args.list:
        print("\\n=== BRIDGES ===")
        for i, bridge in enumerate(bridge_manager.get_all_bridges()):
            current = " [ACTIVE]" if i == bridge_manager.current_index else ""
            print("{i}: {bridge}{current}")

    elif args.deploy:
        print("Deploying new bridge...")
        url = bridge_manager.force_deploy()
        if url:
            print("✅ Deployed: {url}")
        else:
            print("❌ Failed to deploy")

    elif args.test:
        print("Testing {args.test}...")
        if bridge_manager.test_bridge(args.test):
            print("✅ Bridge is working")
        else:
            print("❌ Bridge is dead")

    elif args.remove:
        print("Removing {args.remove}...")
        if bridge_manager.remove_dead_bridge(args.remove):
            print("✅ Removed")
        else:
            print("❌ Not found")

    elif args.stats:
        stats = bridge_manager.get_stats()
        print("\\n=== BRIDGE STATS ===")
        print(json.dumps(stats, indent=2, default=str))

    elif args.switch:
        if bridge_manager._switch_to_next():
            print("✅ Switched to {bridge_manager.get_current_bridge()}")
        else:
            print("❌ Failed to switch")

    else:
        parser.print_help()

if __name__ == "__main__":
    main()