#!/usr/bin/env python

"""
This script converts XML file generated by OpenCover code coverage
tool for .NET into a set of html files for easy browsing of the results.

Usage:
  opencover-to-html.py <opencover-output.xml> <outdir>

<opencover-output.xml> is an XML file with code coverage information
generated by PartCover.

<outdir> is a directory where the output will be stored. A new directory
coverhtml-${NNN} will be created and html files put there, with index.html
being the starting point.
The reason for creating a new directory is to not overwrite results of
previous analysis, so that it's possible to compare coverage before and
after a given change in the coe. ${NNN} is chosen to be unique and is in
increasing order (i.e. 000, 001, 002 etc.)

For reasons of laziness, we mostly just sublaunch ReportGenerator.exe

This code was written by Krzysztof Kowalczyk (http://blog.kowalczyk.info)
and is placed in Public Domain.
"""

import cgi
import os
import os.path
import shutil
import subprocess
import sys
from xml.dom.minidom import parse

def usage_and_exit():
    print("Usage: partcover-to-html.py PARTCOVER_FILE.XML OUTDIR")
    sys.exit(1)

def run_cmd_throw(*args):
  cmd = " ".join(args)
  print("\nrun_cmd_throw: '%s'" % cmd)
  cmdproc = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
  res = cmdproc.communicate()
  errcode = cmdproc.returncode
  if 0 != errcode:
    print("Failed with error code %d" % errcode)
    print("Stdout:")
    print(res[0])
    print("Stderr:")
    print(res[1])
    raise Exception("'%s' failed with error code %d" % (cmd, errcode))
  return (res[0], res[1])

def gen_unique_dir(outdir):
    existing = [dir for dir in os.listdir(outdir) if dir.startswith("coverhtml-")]
    if 0 == len(existing): return os.path.join(outdir, "coverhtml-000")
    existing.sort()
    last_no = int(existing[-1].split("-")[-1])
    no = last_no + 1
    return os.path.join(outdir, "coverhtml-%03d" % no)
    
def main():
    if len(sys.argv) != 3:
        usage_and_exit()
    partcover_file = sys.argv[1]
    outdir = sys.argv[2]
    if not os.path.exists(partcover_file):
        print("File '%s' doesn't exists" % partcover_file)
        print("")
        usage_and_exit()
    if not os.path.exists(outdir):
        os.makedirs(outdir)
    outdir = gen_unique_dir(outdir)
    os.makedirs(outdir)
    shutil.copyfile(partcover_file, os.path.join(outdir, "opencover.xml"))

    report_exe_path = os.path.join("tools", "ReportGenerator", "ReportGenerator.exe")
    run_cmd_throw(report_exe_path, partcover_file, outdir)

if __name__ == "__main__":
    main()
