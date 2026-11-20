import importlib.util
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("preflight", ROOT / "scripts/check-basic-sql.py")
preflight = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preflight)


class PreflightTests(unittest.TestCase):
    def test_resolved_cli_path_is_used(self):
        with patch.object(preflight.shutil, "which", return_value="C:/AzureCLI/az.cmd"), \
                patch.object(preflight.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, "[]")) as run:
            self.assertEqual(preflight.az_json("sql", "server", "list"), [])
            self.assertEqual(run.call_args.args[0], ["C:/AzureCLI/az.cmd", "sql", "server", "list", "-o", "json"])

    def test_cli_start_failure_returns_failure(self):
        with patch.object(preflight.sys, "argv", ["check-basic-sql.py", "rg", "project", "test"]), \
                patch.object(preflight, "check", side_effect=OSError("private details")), \
                patch.object(preflight.sys, "stderr") as stderr:
            self.assertEqual(preflight.main(), 1)
            self.assertNotIn("private details", str(stderr.write.call_args_list))

    def run_check(self, servers=None, databases=None, initial=False):
        with patch.object(preflight, "az_json", side_effect=[
                servers if servers is not None else [{"name": "sql-recipelibrary-test-sec-abc"}],
                databases if databases is not None else []]) as az:
            preflight.check("rg", "recipelibrary", "test-sec", initial)
            self.assertTrue(all(call.args[:3] in [("sql", "server", "list"),
                                                 ("sql", "db", "list")] for call in az.call_args_list))

    def basic(self, **changes):
        return dict(name="recipelibrary-test-sec", sku={"tier": "Basic", "name": "Basic", "capacity": 5},
                    maxSizeBytes=2147483648, useFreeLimit=False, status="Online") | changes

    def test_basic_passes(self):
        self.run_check(databases=[self.basic()])

    def test_wrong_existing_plan_fails_even_with_provisioning(self):
        for initial in (False, True):
            for changes in ({"sku": {"tier": "GeneralPurpose"}}, {"status": "Paused"},
                            {"maxSizeBytes": 2147483649}, {"useFreeLimit": True}):
                with self.subTest(initial=initial, changes=changes), self.assertRaises(ValueError):
                    self.run_check(databases=[self.basic(**changes)], initial=initial)

    def test_missing_requires_explicit_initial_provisioning(self):
        for servers in ([], [{"name": "sql-recipelibrary-test-sec-abc"}]):
            with self.assertRaises(ValueError):
                self.run_check(servers=servers)
            self.run_check(servers=servers, initial=True)

    def test_ambiguous_servers_fail_even_with_provisioning(self):
        with self.assertRaises(ValueError):
            self.run_check(servers=[{"name": "sql-recipelibrary-test-sec-a"},
                                    {"name": "sql-recipelibrary-test-sec-b"}], initial=True)

    def test_management_failure_cannot_be_treated_as_missing(self):
        with patch.object(preflight, "az_json", side_effect=subprocess.CalledProcessError(1, "az")):
            with self.assertRaises(subprocess.CalledProcessError):
                preflight.check("rg", "recipelibrary", "test-sec", True)

    def test_pipeline_preflight_precedes_first_arm_deployment(self):
        pipeline = (ROOT / "azure-pipelines.yml").read_text()
        self.assertLess(pipeline.index("python3 scripts/check-basic-sql.py"),
                        pipeline.index("az deployment group create"))
        self.assertIn('--allow-initial ${{ lower(parameters.provisionSqlInfrastructure) }}', pipeline)
        self.assertIn('python3 -m unittest discover -s tests/infra', pipeline)


if __name__ == "__main__":
    unittest.main()
