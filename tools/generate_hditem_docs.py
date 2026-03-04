#!/usr/bin/env python3

from __future__ import annotations

import argparse
import os
import re
import textwrap
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path


EXCLUDE_DIRS = {"bin", "obj", ".git", ".vs", "packages"}


@dataclass
class MemberDoc:
    kind: str
    signature: str


@dataclass
class TypeDoc:
    name: str
    kind: str
    modifier: str
    bases: list[str] = field(default_factory=list)
    summary: str = ""
    members: list[MemberDoc] = field(default_factory=list)


def _clean_xml_namespace(tag: str) -> str:
    if "}" in tag:
        return tag.split("}", 1)[1]
    return tag


def parse_csproj_metadata(csproj_path: Path) -> dict[str, str]:
    metadata: dict[str, str] = {}
    try:
        tree = ET.parse(csproj_path)
    except ET.ParseError:
        return metadata

    root = tree.getroot()
    properties: dict[str, str] = {}
    for node in root.iter():
        tag = _clean_xml_namespace(node.tag)
        if tag in {
            "AssemblyName",
            "RootNamespace",
            "TargetFramework",
            "TargetFrameworks",
            "TargetFrameworkVersion",
            "OutputType",
            "PackageId",
            "Version",
            "FileVersion",
        }:
            value = (node.text or "").strip()
            if value and tag not in properties:
                properties[tag] = value

    framework = (
        properties.get("TargetFramework")
        or properties.get("TargetFrameworks")
        or properties.get("TargetFrameworkVersion")
        or "unknown"
    )
    metadata["AssemblyName"] = properties.get("AssemblyName", "")
    metadata["RootNamespace"] = properties.get("RootNamespace", "")
    metadata["TargetFramework"] = framework
    metadata["OutputType"] = properties.get("OutputType", "Library")
    metadata["PackageId"] = properties.get("PackageId", "")
    metadata["Version"] = properties.get("Version", properties.get("FileVersion", ""))
    return metadata


def discover_source_files(folder: Path) -> list[Path]:
    files: list[Path] = []
    for root, dirs, names in os.walk(folder):
        dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS]
        for name in names:
            if not name.endswith(".cs"):
                continue
            files.append(Path(root) / name)
    return sorted(files)


TYPE_PATTERN = re.compile(
    r"^\s*(public|internal)\s+"
    r"(?:(static|abstract|sealed|partial)\s+)*"
    r"(class|interface|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)"
    r"(?:\s*:\s*([^\{]+))?",
    re.MULTILINE,
)

SUMMARY_PATTERN = re.compile(
    r"///\s*<summary>\s*(.*?)\s*///\s*</summary>",
    re.DOTALL,
)

MEMBER_PATTERN = re.compile(
    r"^\s*public\s+(?:static\s+)?(?:virtual\s+|override\s+|abstract\s+|sealed\s+)?"
    r"([^\n;\{]+(?:\([^\)]*\))?[^\n;\{]*)",
    re.MULTILINE,
)


def _extract_summary_before(content: str, position: int) -> str:
    chunk = content[max(0, position - 700):position]
    matches = list(SUMMARY_PATTERN.finditer(chunk))
    if not matches:
        return ""
    summary = re.sub(r"\s+", " ", matches[-1].group(1)).strip()
    return summary


def _normalize_member_signature(raw: str) -> tuple[str, str]:
    signature = re.sub(r"\s+", " ", raw).strip()
    if "(" in signature and ")" in signature:
        return "Method", signature
    if "{" in signature and "}" in signature:
        return "Property", signature
    if " get;" in signature or " set;" in signature:
        return "Property", signature
    return "Member", signature


def parse_types(file_path: Path) -> list[TypeDoc]:
    content = file_path.read_text(encoding="utf-8", errors="ignore")
    types: list[TypeDoc] = []
    for match in TYPE_PATTERN.finditer(content):
        modifier = match.group(1)
        kind = match.group(3)
        name = match.group(4)
        bases_raw = (match.group(5) or "").strip()
        bases = []
        if bases_raw:
            for base in bases_raw.split(","):
                b = base.strip().split("<", 1)[0].strip()
                if b:
                    bases.append(b)

        summary = _extract_summary_before(content, match.start())

        body_start = content.find("{", match.end())
        body_end = content.find("\n}", body_start)
        if body_start == -1:
            body = ""
        elif body_end == -1:
            body = content[body_start:]
        else:
            body = content[body_start:body_end]

        members: list[MemberDoc] = []
        for mm in MEMBER_PATTERN.finditer(body):
            kind_name, sig = _normalize_member_signature(mm.group(1))
            members.append(MemberDoc(kind=kind_name, signature=sig))

        types.append(
            TypeDoc(
                name=name,
                kind=kind,
                modifier=modifier,
                bases=bases,
                summary=summary,
                members=members,
            )
        )
    return types


def safe_package_name(name: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_.-]", "_", name).strip("_")
    return cleaned or "Package"


def pick_package_name(folder: Path, csproj_files: list[Path], sln_files: list[Path], metadata: dict[str, str]) -> str:
    return (
        metadata.get("AssemblyName")
        or metadata.get("PackageId")
        or (csproj_files[0].stem if csproj_files else "")
        or (sln_files[0].stem if sln_files else "")
        or folder.name
        or "Package"
    )


def build_readme(
    package_name: str,
    folder: Path,
    sln_files: list[Path],
    csproj_files: list[Path],
    metadata: dict[str, str],
    source_files: list[Path],
    typedocs: dict[str, list[TypeDoc]],
    diagram_name: str,
) -> str:
    framework = metadata.get("TargetFramework", "unknown")
    output_type = metadata.get("OutputType", "Library")
    root_ns = metadata.get("RootNamespace", "")
    version = metadata.get("Version", "")

    overview_bits = [f"`{package_name}` is a .NET project in the 2017 HDitem/ImageServer codebase."]
    overview_bits.append(f"Output type: `{output_type}`.")
    if framework and framework != "unknown":
        overview_bits.append(f"Target framework: `{framework}`.")
    if root_ns:
        overview_bits.append(f"Root namespace: `{root_ns}`.")
    overview = " ".join(overview_bits)

    lines: list[str] = [f"# {package_name}", "", "## Overview", "", overview, ""]
    if version:
        lines.extend(["## Version", "", f"- `{version}`", ""])

    lines.extend(["## Solution and Project Metadata", ""])
    if sln_files:
        lines.append("- **Solutions**")
        for sln in sln_files:
            lines.append(f"  - `{sln.name}`")
    if csproj_files:
        lines.append("- **Projects**")
        for cs in csproj_files:
            lines.append(f"  - `{cs.name}`")

    lines.append(f"- **Output type**: `{output_type}`")
    lines.append(f"- **Target framework**: `{framework}`")
    if root_ns:
        lines.append(f"- **Root namespace**: `{root_ns}`")
    lines.append("")

    lines.extend(["## Source Inventory", ""])
    if source_files:
        for path in source_files:
            rel = path.relative_to(folder).as_posix()
            lines.append(f"- `{rel}`")
    else:
        lines.append("- No C# source files found in this folder tree.")
    lines.append("")

    lines.extend(["## Class Documentation", ""])
    if not typedocs:
        lines.append("No public class/interface/struct/enum declarations detected.")
        lines.append("")
    else:
        for relfile, classes in sorted(typedocs.items()):
            for td in classes:
                title = f"<strong>{td.name}</strong> ({td.kind}) — <code>{relfile}</code>"
                lines.append("<details>")
                lines.append(f"<summary>{title}</summary>")
                lines.append("")
                lines.append("### Class comment")
                lines.append("")
                lines.append(td.summary if td.summary else "No XML class summary.")
                lines.append("")
                lines.append("### Public members")
                lines.append("")
                if td.members:
                    for member in td.members:
                        lines.append(f"- `{member.signature}`")
                else:
                    lines.append("- No public members found by static scan.")
                lines.append("")
                lines.append("</details>")
                lines.append("")

    lines.extend(
        [
            "## Diagram",
            "",
            f"- Source: [{diagram_name}]({diagram_name})",
            f"- CLI render (if PlantUML is installed): `plantuml {diagram_name}`",
            "",
        ]
    )

    return "\n".join(lines).rstrip() + "\n"


def to_puml(package_name: str, typedocs: dict[str, list[TypeDoc]]) -> str:
    local_names = set()
    for items in typedocs.values():
        for td in items:
            local_names.add(td.name)

    lines = [
        f"@startuml {package_name}",
        "",
        "skinparam classAttributeIconSize 0",
        "skinparam shadowing false",
        "",
        f'package "{package_name}" {{',
        "",
    ]

    relations: list[str] = []
    for items in typedocs.values():
        for td in items:
            stereotype = " <<static>>" if any(m.signature.startswith("static ") for m in td.members) else ""
            lines.append(f"class {td.name}{stereotype} {{")
            if td.members:
                for member in td.members[:60]:
                    member_line = member.signature.replace("{", "").replace("}", "")
                    lines.append(f"  +{member_line}")
            lines.append("}")
            lines.append("")

            for base in td.bases:
                base_simple = base.split(".")[-1]
                if base_simple in local_names:
                    relations.append(f"{td.name} --|> {base_simple}")

    lines.append("}")
    lines.append("")
    for rel in sorted(set(relations)):
        lines.append(rel)
    lines.append("")
    lines.append("@enduml")
    lines.append("")
    return "\n".join(lines)


def target_folders(root: Path) -> list[Path]:
    folders: set[Path] = set()
    for current, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS]
        if any(name.endswith(".sln") or name.endswith(".csproj") for name in files):
            folders.add(Path(current))
    return sorted(folders)


def generate_for_folder(folder: Path) -> tuple[Path, Path]:
    sln_files = sorted(folder.glob("*.sln"))
    csproj_files = sorted(folder.glob("*.csproj"))

    metadata = parse_csproj_metadata(csproj_files[0]) if csproj_files else {}
    package_name = safe_package_name(pick_package_name(folder, csproj_files, sln_files, metadata))
    diagram_name = f"{package_name}_cd.puml"

    source_files = discover_source_files(folder)
    typedocs: dict[str, list[TypeDoc]] = {}
    for src in source_files:
        items = parse_types(src)
        if items:
            rel = src.relative_to(folder).as_posix()
            typedocs[rel] = items

    readme = build_readme(
        package_name=package_name,
        folder=folder,
        sln_files=sln_files,
        csproj_files=csproj_files,
        metadata=metadata,
        source_files=source_files,
        typedocs=typedocs,
        diagram_name=diagram_name,
    )
    puml = to_puml(package_name, typedocs)

    readme_path = folder / "README.md"
    puml_path = folder / diagram_name
    readme_path.write_text(readme, encoding="utf-8")
    puml_path.write_text(puml, encoding="utf-8")
    return readme_path, puml_path


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Generate README.md and <PackageName>_cd.puml for folders containing .sln/.csproj"
    )
    parser.add_argument("root", type=Path, help="Root directory to scan")
    args = parser.parse_args()

    root = args.root.expanduser().resolve()
    if not root.exists() or not root.is_dir():
        raise SystemExit(f"Root path does not exist or is not a directory: {root}")

    folders = target_folders(root)
    if not folders:
        raise SystemExit("No target folders with .sln/.csproj found")

    created = 0
    for folder in folders:
        readme_path, puml_path = generate_for_folder(folder)
        print(f"Generated: {readme_path} | {puml_path}")
        created += 2

    print(textwrap.dedent(f"""
    Done.
      Root: {root}
      Target folders: {len(folders)}
      Files written: {created}
    """).strip())


if __name__ == "__main__":
    main()
