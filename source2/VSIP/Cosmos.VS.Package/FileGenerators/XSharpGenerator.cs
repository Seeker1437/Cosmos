﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.IO;

namespace Cosmos.VS.Package.FileGenerators
{
    public class XSharpGenerator : IVsSingleFileGenerator
    {
        public int DefaultExtension(out string pbstrDefaultExtension)
        {
            pbstrDefaultExtension = ".xs";
            return VSConstants.S_OK;
        }

        public int Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace, IntPtr[] rgbOutputFileContents,
            out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
        {
            var xGeneratedCode = DoGenerate(wszInputFilePath, bstrInputFileContents, wszDefaultNamespace);
            var xBytes = Encoding.UTF8.GetBytes(xGeneratedCode);

            if (xBytes.Length > 0)
            {
                rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(xBytes.Length);
                Marshal.Copy(xBytes, 0, rgbOutputFileContents[0], xBytes.Length);
                pcbOutput = (uint)xBytes.Length;
            }
            else
            {
                rgbOutputFileContents[0] = IntPtr.Zero;
                pcbOutput = 0;
            }
            return VSConstants.S_OK;
        }

        private static string DoGenerate(string inputFileName, string inputFileContents, string defaultNamespace)
        {
            using (var xInput = new StringReader(inputFileContents))
            {
                using (var xOut = new StringWriter())
                {
                    Cosmos.XSharp.Generator.Generate(xInput, inputFileName, xOut, defaultNamespace);
                    return xOut.ToString();
                }
            }
        }
    }
}