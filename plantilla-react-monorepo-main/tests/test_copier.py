import json
import shutil
import tempfile
import unittest
from pathlib import Path

import yaml
from copier import run_copy


ROOT = Path(__file__).resolve().parents[1]


class CopierTemplateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        temporary = tempfile.TemporaryDirectory(prefix="copier-template-tests-")
        cls.addClassCleanup(temporary.cleanup)
        cls.workspace = Path(temporary.name)
        cls.source = cls.workspace / "template"
        shutil.copytree(
            ROOT,
            cls.source,
            ignore=shutil.ignore_patterns(
                ".git", "node_modules", ".turbo", "dist", "__pycache__", ".venv"
            ),
        )
        for relative_path in (
            "node_modules/copier-sentinel.txt",
            "apps/webapp/dist/copier-sentinel.txt",
            "paquetes/api/.turbo/copier-sentinel.txt",
            ".env.local",
            "apps/webapp/entorno/.env.local",
        ):
            sentinel = cls.source / relative_path
            sentinel.parent.mkdir(parents=True, exist_ok=True)
            sentinel.write_text("local-only", encoding="utf-8")

    def render(self, **data):
        destination = self.workspace / self.id().rsplit(".", 1)[-1]
        run_copy(
            str(self.source), destination, data=data, defaults=True, quiet=True
        )
        return destination

    def test_defaults_preserve_structure_and_dependencies(self):
        destination = self.render()
        expected = json.loads((ROOT / "package.json").read_text(encoding="utf-8"))
        expected.update(
            name="mi-proyecto",
            description="Monorepo React con Vite, Turborepo y pnpm.",
        )
        manifest = json.loads(
            (destination / "package.json").read_text(encoding="utf-8")
        )
        self.assertEqual(manifest, expected)
        for relative_path in (
            "apps/webapp/vite.config.ts",
            "apps/webapp/src/App.tsx",
            "paquetes/api/package.json",
            "paquetes/componentes/package.json",
            "paquetes/configuracion-ts/package.json",
            "paquetes/estados/package.json",
            "paquetes/hooks/package.json",
            "paquetes/modelos/package.json",
            "paquetes/utiles/package.json",
            "pnpm-lock.yaml",
            "pnpm-workspace.yaml",
            ".npmrc",
            ".nvmrc",
        ):
            with self.subTest(path=relative_path):
                self.assertEqual(
                    (destination / relative_path).read_bytes(),
                    (ROOT / relative_path).read_bytes(),
                )

    def test_custom_values_are_rendered_and_escaped(self):
        name = 'Portal <clientes> & "equipo"'
        description = 'Descripcion con "comillas", \\rutas y salto\nde linea.'
        destination = self.render(
            project_name=name,
            project_slug="portal-clientes",
            project_description=description,
        )
        manifest = json.loads(
            (destination / "package.json").read_text(encoding="utf-8")
        )
        self.assertEqual(manifest["name"], "portal-clientes")
        self.assertEqual(manifest["description"], description)
        html = (destination / "apps/webapp/index.html").read_text(encoding="utf-8")
        self.assertIn(
            "<title>Portal &lt;clientes&gt; &amp; &#34;equipo&#34;</title>", html
        )
        readme = (destination / "README.md").read_text(encoding="utf-8")
        self.assertIn(f"# {name}", readme)
        self.assertIn(description, readme)
        answers = yaml.safe_load(
            (destination / ".copier-answers.yml").read_text(encoding="utf-8")
        )
        self.assertEqual(answers["project_slug"], "portal-clientes")
        self.assertEqual(answers["project_name"], name)

    def test_template_metadata_and_local_artifacts_are_excluded(self):
        destination = self.render()
        for relative_path in (
            "copier.yml",
            "tests/test_copier.py",
            "node_modules",
            "apps/webapp/dist",
            "paquetes/api/.turbo",
            ".env.local",
            "apps/webapp/entorno/.env.local",
        ):
            with self.subTest(path=relative_path):
                self.assertFalse((destination / relative_path).exists())
        self.assertEqual(list(destination.rglob("*.jinja")), [])

    def test_invalid_slug_is_rejected(self):
        for slug in ("../escape", "Con Espacios", "-proyecto", "a" * 215):
            with self.subTest(slug=slug), self.assertRaises(ValueError):
                self.render(project_slug=slug)

    def test_git_source_records_revision_for_updates(self):
        destination = self.workspace / "versioned-project"
        run_copy(
            str(ROOT), destination, vcs_ref="HEAD", defaults=True, quiet=True
        )
        answers = yaml.safe_load(
            (destination / ".copier-answers.yml").read_text(encoding="utf-8")
        )
        self.assertTrue(answers["_commit"])
        self.assertTrue(answers["_src_path"])


if __name__ == "__main__":
    unittest.main()