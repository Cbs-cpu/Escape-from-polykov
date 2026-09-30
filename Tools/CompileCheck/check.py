#!/usr/bin/env python3
"""
Type-checks every Unity assembly definition under Assets/_Project without opening Unity.

For each .asmdef it generates a throwaway .csproj (in .gen/) that compiles exactly the scripts Unity
would put in that assembly and references only the assemblies the asmdef declares, so a missing
asmdef reference or a typo fails here the same way it would in the editor.

UnityEngine/UnityEditor come from reference-assembly NuGet packages (2021.3 API; Unity 6-only APIs
may need to be added to Stubs/). com.unity.inputsystem is replaced by Stubs/InputSystemStubs.cs.

Usage:  python3 Tools/CompileCheck/check.py            (runtime + editor + tests)
Needs:  .NET SDK 8 and network access to nuget.org on first run.
"""
import json
import pathlib
import shutil
import subprocess
import sys

# Errors caused by the old reference assemblies, not by our code (API is settable in Unity 6).
KNOWN_FALSE_POSITIVES = [
    "'AnimatorControllerParameter.name' cannot be assigned to",
]

# Unity 6 renamed some APIs; the offline check compiles against 2021.3 reference DLLs, so the sources are
# translated (whole-word, line for line) into a temporary copy. Add entries here when a new Unity 6 name shows up.
UNITY6_TO_2021 = {
    "linearVelocity": "velocity",
    "linearDamping": "drag",
    "angularDamping": "angularDrag",
    "PhysicsMaterialCombine": "PhysicMaterialCombine",
    "PhysicsMaterial": "PhysicMaterial",
    "FindFirstObjectByType": "FindObjectOfType",
    "FindAnyObjectByType": "FindObjectOfType",
}

ROOT = pathlib.Path(__file__).resolve().parents[2]
HERE = pathlib.Path(__file__).resolve().parent
GEN = HERE / ".gen"
SCRIPTS = ROOT / "Assets" / "_Project"

ENGINE_PKG = '<PackageReference Include="UnityEngine.Modules" Version="2021.3.33" PrivateAssets="all" />'
EDITOR_PKG = '<PackageReference Include="Unity3D.SDK" Version="2021.1.14.1" PrivateAssets="all" />'
NUNIT_PKG = '<PackageReference Include="NUnit" Version="3.14.0" />'
FRAMEWORK_PKG = ('<PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" '
                 'PrivateAssets="all" />')

# asmdef references that are satisfied by something other than a project asmdef.
EXTERNAL = {
    "Unity.InputSystem": "stub",
    "UnityEngine.TestRunner": "nunit",
    "UnityEditor.TestRunner": "nunit",
}


def load_asmdefs():
    asmdefs = {}
    for path in SCRIPTS.rglob("*.asmdef"):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        asmdefs[data["name"]] = (path.parent, data)
    return asmdefs


def sources_for(folder, all_folders):
    """All .cs under folder, except those that belong to a nested asmdef."""
    nested = [f for f in all_folders if f != folder and folder in f.parents]
    for cs in sorted(folder.rglob("*.cs")):
        if any(n in cs.parents for n in nested):
            continue
        yield cs


def translated(cs, assembly):
    """Copy of a source file with Unity 6 API names mapped to their 2021.3 equivalents (returns the copy's path)."""
    import re
    text = cs.read_text(encoding="utf-8-sig")
    for new, old in UNITY6_TO_2021.items():
        text = re.sub(rf"\b{new}\b", old, text)
    out = GEN / assembly / "src" / cs.relative_to(ROOT)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text, encoding="utf-8")
    return out


def dll_ref(folder, assembly):
    """Plain DLL reference (not ProjectReference): asmdef references are NOT transitive, MSBuild's would be."""
    return (f'<Reference Include="{assembly}"><HintPath>../{folder}/bin/Debug/net472/{assembly}.dll</HintPath>'
            f'<Private>false</Private></Reference>')


def build_order(asmdefs):
    """Dependencies first."""
    order, seen = [], set()

    def visit(name):
        if name in seen or name not in asmdefs:
            return
        seen.add(name)
        for ref in asmdefs[name][1].get("references", []):
            visit(ref)
        order.append(name)

    for name in sorted(asmdefs):
        visit(name)
    return order


def project_xml(name, data, folder, all_folders, names):
    is_editor = data.get("includePlatforms") == ["Editor"]
    refs, packages = [], [ENGINE_PKG, FRAMEWORK_PKG]
    defines = ["UNITY_2021_3_OR_NEWER", "UNITY_6000_0_OR_NEWER", "POLYKOV_COMPILE_CHECK"]
    if is_editor:
        packages.append(EDITOR_PKG)
        defines.append("UNITY_EDITOR")
    for ref in data.get("references", []):
        ref = ref.split(":", 1)[-1] if ref.startswith("GUID:") else ref
        kind = EXTERNAL.get(ref)
        if kind == "stub":
            refs.append(dll_ref("_InputSystemStub", "Unity.InputSystem"))
        elif kind == "nunit":
            if NUNIT_PKG not in packages:
                packages.append(NUNIT_PKG)
                packages.append(EDITOR_PKG) if EDITOR_PKG not in packages else None
        elif ref in names:
            refs.append(dll_ref(ref, ref))
        else:
            print(f"warning: {name} references unknown assembly '{ref}'", file=sys.stderr)
    if "nunit.framework.dll" in data.get("precompiledReferences", []) and NUNIT_PKG not in packages:
        packages.append(NUNIT_PKG)
    compiles = "\n    ".join(f'<Compile Include="{translated(cs, name)}" />' for cs in sources_for(folder, all_folders))
    return f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>{name}</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AllowUnsafeBlocks>{str(data.get("allowUnsafeCode", False)).lower()}</AllowUnsafeBlocks>
    <DefineConstants>{";".join(defines)}</DefineConstants>
    <NoWarn>NU1701;CS0414;CS0649;CS0169</NoWarn>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <!-- Unity asmdef references are NOT transitive; MSBuild's are by default. Match Unity. -->
    <DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>
  </PropertyGroup>
  <ItemGroup>
    {compiles}
  </ItemGroup>
  <ItemGroup>
    {chr(10).join("    " + p for p in packages).strip()}
    {chr(10).join("    " + r for r in refs).strip()}
  </ItemGroup>
</Project>
"""


def main():
    asmdefs = load_asmdefs()
    if GEN.exists():
        shutil.rmtree(GEN)
    GEN.mkdir()
    stub = GEN / "_InputSystemStub"
    stub.mkdir()
    (stub / "_InputSystemStub.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <AssemblyName>Unity.InputSystem</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>NU1701;CS0067</NoWarn>
  </PropertyGroup>
  <ItemGroup><Compile Include="{HERE / 'Stubs' / 'InputSystemStubs.cs'}" /></ItemGroup>
  <ItemGroup>{ENGINE_PKG}{FRAMEWORK_PKG}</ItemGroup>
</Project>
""")
    folders = [f for f, _ in asmdefs.values()]
    lines = ["Microsoft Visual Studio Solution File, Format Version 12.00"]
    projects = []
    for name in build_order(asmdefs):
        folder, data = asmdefs[name]
        d = GEN / name
        d.mkdir()
        (d / f"{name}.csproj").write_text(project_xml(name, data, folder, folders, asmdefs))
        projects.append(d / f"{name}.csproj")

    failed = []
    stub_build = subprocess.run(["dotnet", "build", str(stub / "_InputSystemStub.csproj"), "-nologo", "-v:q"],
                                capture_output=True, text=True)
    if stub_build.returncode != 0:
        print(stub_build.stdout[-2000:])
        return 1
    for proj in projects:
        r = subprocess.run(["dotnet", "build", str(proj), "-nologo", "-v:q", "-clp:NoSummary"],
                           capture_output=True, text=True)
        errors = [l for l in r.stdout.splitlines() if ": error " in l]
        real = [l for l in errors if not any(k in l for k in KNOWN_FALSE_POSITIVES)]
        ok = r.returncode == 0 or (errors and not real)
        status = "OK " if ok else "ERR"
        print(f"[{status}] {proj.stem}")
        if not ok:
            failed.append(proj.stem)
            seen = set()
            for l in real or r.stdout.splitlines()[-20:]:
                l = l.split(" [", 1)[0].replace(str(ROOT) + "/", "").replace(f"Tools/CompileCheck/.gen/{proj.stem}/src/", "")
                if l not in seen:
                    seen.add(l)
                    print("      " + l)
    if failed:
        print(f"\n{len(failed)} assembly(ies) failed: {', '.join(failed)}")
        return 1
    print(f"\nAll {len(projects)} assemblies compile.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
