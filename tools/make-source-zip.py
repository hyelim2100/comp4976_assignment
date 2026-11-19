"""Creates the source archive for submission and (with --verify) proves it builds and passes the tests.

    python tools/make-source-zip.py            -> ../MEMS-A2-source.zip
    python tools/make-source-zip.py --verify   -> also extracts it into an empty temp folder,
                                                  runs `dotnet build MEMS.sln` and `dotnet test MEMS.Tests`

Excluded: bin/, obj/, .vs/, .git/, node_modules/, TestResults/, _spec/ (local course files), *.user, *.zip,
Office lock files. Everything is placed under a top-level MEMS/ folder.
"""
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
OUT = os.path.abspath(os.path.join(ROOT, '..', 'MEMS-A2-source.zip'))
SKIP_DIRS = {'bin', 'obj', '.vs', '.git', 'node_modules', '_spec', 'TestResults', '.idea'}
SKIP_FILES = ('.user', '.suo', '.zip')


def create():
    count = 0
    with zipfile.ZipFile(OUT, 'w', zipfile.ZIP_DEFLATED) as archive:
        for folder, dirs, files in os.walk(ROOT):
            dirs[:] = sorted(d for d in dirs if d not in SKIP_DIRS and not d.startswith('mems-e2e-'))
            for name in sorted(files):
                if name.endswith(SKIP_FILES) or name.startswith('~$'):
                    continue
                path = os.path.join(folder, name)
                archive.write(path, os.path.join('MEMS', os.path.relpath(path, ROOT)))
                count += 1
    with zipfile.ZipFile(OUT) as archive:
        leaked = [n for n in archive.namelist() if any(f'/{d}/' in n for d in SKIP_DIRS)]
    if leaked:
        sys.exit(f'Excluded folders found in the archive: {leaked[:5]}')
    print(f'{OUT}: {count} files, {os.path.getsize(OUT) / 1024 / 1024:.1f} MB, no bin/obj/.git')


def verify():
    target = tempfile.mkdtemp(prefix='mems-zip-check-')
    try:
        with zipfile.ZipFile(OUT) as archive:
            archive.extractall(target)
        solution = os.path.join(target, 'MEMS')
        for command in (['dotnet', '--version'],
                        ['dotnet', 'build', 'MEMS.sln', '-nologo', '-v', 'q'],
                        ['dotnet', 'test', 'MEMS.Tests', '--no-build', '-nologo', '-v', 'q']):
            print('>', ' '.join(command), flush=True)
            result = subprocess.run(command, cwd=solution)
            if result.returncode != 0:
                sys.exit(f'FAILED: {" ".join(command)}')
        print('Archive verified: builds and all tests pass from a clean extraction.')
    finally:
        shutil.rmtree(target, ignore_errors=True)


if __name__ == '__main__':
    create()
    if '--verify' in sys.argv:
        verify()
