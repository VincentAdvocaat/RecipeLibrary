"""Fail closed before SEC deployment using Azure management metadata only."""

import argparse
import json
import shutil
import subprocess
import sys


def az_json(*args):
    result = subprocess.run([shutil.which("az") or "az", *args, "-o", "json"], check=True,
                            capture_output=True, text=True)
    return json.loads(result.stdout)


def check(resource_group, project, environment, allow_initial=False):
    prefix = f"sql-{project}-{environment}-".lower()
    database_name = f"{project}-{environment}".lower()
    servers = [server for server in az_json("sql", "server", "list", "-g", resource_group)
               if server["name"].startswith(prefix)]
    if not servers and allow_initial:
        print("No existing SQL server; explicit initial provisioning allowed.")
        return
    if len(servers) != 1:
        raise ValueError("Expected exactly one matching SQL server; initial provisioning must be explicit.")
    databases = [db for db in az_json("sql", "db", "list", "-g", resource_group,
                                    "-s", servers[0]["name"])
                 if db["name"] == database_name]
    if not databases and allow_initial:
        print("No existing application database; explicit initial provisioning allowed.")
        return
    if len(databases) != 1:
        raise ValueError("Expected the application database; initial provisioning must be explicit.")
    db = databases[0]
    sku = db.get("sku", {})
    size = db.get("maxSizeBytes")
    if not (sku.get("tier") == "Basic" and sku.get("name") == "Basic"
            and sku.get("capacity") == 5 and isinstance(size, int)
            and 0 < size <= 2147483648 and db.get("useFreeLimit") is not True
            and db.get("status") == "Online"):
        raise ValueError("Expected Online SQL Basic 5 DTU / <=2 GiB without free offer. "
                         "Run the reviewed conversion first; provisioning cannot bypass this check.")
    print("Approved Online SQL Basic plan verified before deployment.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("resource_group")
    parser.add_argument("project")
    parser.add_argument("environment")
    parser.add_argument("--allow-initial", choices=("true", "false"), default="false")
    args = parser.parse_args()
    try:
        check(args.resource_group, args.project, args.environment, args.allow_initial == "true")
    except OSError:
        print("SQL preflight failed: Azure CLI could not be started.", file=sys.stderr)
        return 1
    except (ValueError, KeyError, TypeError, subprocess.CalledProcessError) as error:
        print(f"SQL preflight failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
