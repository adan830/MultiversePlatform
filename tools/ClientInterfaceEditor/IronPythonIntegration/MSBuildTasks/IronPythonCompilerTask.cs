/********************************************************************

The Multiverse Platform is made available under the MIT License.

Copyright (c) 2012 The Multiverse Foundation

Permission is hereby granted, free of charge, to any person 
obtaining a copy of this software and associated documentation 
files (the "Software"), to deal in the Software without restriction, 
including without limitation the rights to use, copy, modify, 
merge, publish, distribute, sublicense, and/or sell copies 
of the Software, and to permit persons to whom the Software 
is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be 
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES 
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE 
OR OTHER DEALINGS IN THE SOFTWARE.

*********************************************************************/

/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Samples.VisualStudio.IronPythonTasks.Properties;

namespace Microsoft.Samples.VisualStudio.IronPythonTasks
{
	/////////////////////////////////////////////////////////////////////////////
	// My MSBuild Task
	public class IronPythonCompilerTask : Task
	{
		private ICompiler compiler = null;

		#region Constructors
		/// <summary>
		/// Constructor. This is the constructor that will be used
		/// when the task run.
		/// </summary>
		public IronPythonCompilerTask()
		{
		}

		/// <summary>
		/// Constructor. The goal of this constructor is to make
		/// it easy to test the task.
		/// </summary>
		public IronPythonCompilerTask(ICompiler compilerToUse)
		{
			compiler = compilerToUse;
		}
		#endregion

		#region Public Properties and related Fields
		private string[] sourceFiles;
		/// <summary>
		/// List of Python source files that should be compiled into the assembly
		/// </summary>
		[Required()]
		public string[] SourceFiles
		{
			get { return sourceFiles; }
			set { sourceFiles = value; }
		}

		private string outputAssembly;
		/// <summary>
		/// Output Assembly (including extension)
		/// </summary>
		[Required()]
		public string OutputAssembly
		{
			get { return outputAssembly; }
			set { outputAssembly = value; }
		}

		private ITaskItem[] referencedAssemblies = new ITaskItem[0];
		/// <summary>
		/// List of dependent assemblies
		/// </summary>
		public ITaskItem[] ReferencedAssemblies
		{
			get { return referencedAssemblies; }
			set
			{
				if (value != null)
				{
					referencedAssemblies = value;
				}
				else
				{
					referencedAssemblies = new ITaskItem[0];
				}

			}
		}

		private ITaskItem[] resourceFiles = new ITaskItem[0];
		/// <summary>
		/// List of resource files
		/// </summary>
		public ITaskItem[] ResourceFiles
		{
			get { return resourceFiles; }
			set
			{
				if (value != null)
				{
					resourceFiles = value;
				}
				else
				{
					resourceFiles = new ITaskItem[0];
				}

			}
		}

		private string mainFile;
		/// <summary>
		/// For applications, which file is the entry point
		/// </summary>
		[Required()]
		public string MainFile
		{
			get { return mainFile; }
			set { mainFile = value; }
		}

		private string targetKind;
		/// <summary>
		/// Target type (exe, winexe, library)
		/// These will be mapped to System.Reflection.Emit.PEFileKinds
		/// </summary>
		public string TargetKind
		{
			get { return targetKind; }
			set { targetKind = value.ToLower(CultureInfo.InvariantCulture); }
		}
		private bool debugSymbols = true;
		/// <summary>
		/// Generate debug information
		/// </summary>
		public bool DebugSymbols
		{
			get { return debugSymbols; }
			set { debugSymbols = value; }
		}
		private string projectPath = null;
		/// <summary>
		/// This should be set to $(MSBuildProjectDirectory)
		/// </summary>
		public string ProjectPath
		{
			get { return projectPath; }
			set { projectPath = value; }
		}

		private bool useExperimentalCompiler;
		/// <summary>
		/// This property is only needed because Iron Python does not officially support building real .Net assemblies.
		/// For WAP scenarios, we need to support real assemblies and as such we use an alternate approach to build those assemblies.
		/// </summary>
		public bool UseExperimentalCompiler
		{
			get { return useExperimentalCompiler; }
			set { useExperimentalCompiler = value; }
		}
	
		#endregion

		/// <summary>
		/// Main entry point for the task
		/// </summary>
		/// <returns></returns>
		public override bool Execute()
		{
			Log.LogMessage(MessageImportance.Normal, "Iron Python Compilation Task");

			// Create the compiler if it does not already exist
			CompilerErrorSink errorSink = new CompilerErrorSink(this.Log);
			errorSink.ProjectDirectory = ProjectPath;
			if (compiler == null)
			{
				if (UseExperimentalCompiler)
					compiler = new ExperimentalCompiler(new List<string>(this.SourceFiles), this.OutputAssembly, errorSink);
				else
					compiler = new Compiler(new List<string>(this.SourceFiles), this.OutputAssembly, errorSink);
			}

			if (!InitializeCompiler())
				return false;

			// Call the compiler and report errors and warnings
			compiler.Compile();

			return errorSink.BuildSucceeded;
		}

		/// <summary>
		/// Initialize compiler options based on task parameters
		/// </summary>
		/// <returns>false if failed</returns>
		private bool InitializeCompiler()
		{
			switch (TargetKind)
			{
				case "exe":
					{
						compiler.TargetKind = System.Reflection.Emit.PEFileKinds.ConsoleApplication;
						break;
					}
				case "winexe":
					{
						compiler.TargetKind = System.Reflection.Emit.PEFileKinds.WindowApplication;
						break;
					}
				case "library":
					{
						compiler.TargetKind = System.Reflection.Emit.PEFileKinds.Dll;
						break;
					}
				default:
					{
						this.Log.LogError(Resources.InvalidTargetType, TargetKind);
						return false;
					}
			}
			compiler.IncludeDebugInformation = this.DebugSymbols;
			compiler.MainFile = this.MainFile;
			compiler.SourceFiles = new List<string>(this.SourceFiles);

			// References require a bit more work since our compiler expect us to pass the Assemblies (and not just paths)
			compiler.ReferencedAssemblies = new List<string>();
			foreach (ITaskItem assemblyReference in this.ReferencedAssemblies)
			{
				compiler.ReferencedAssemblies.Add(assemblyReference.ItemSpec);
			}

			// Add each resource
			List<IronPython.Hosting.ResourceFile> resourcesList = new List<IronPython.Hosting.ResourceFile>();
			foreach (ITaskItem resource in this.ResourceFiles)
			{
				bool publicVisibility = true;
				string access = resource.GetMetadata("Access");
				if (String.CompareOrdinal("Private", access) == 0)
					publicVisibility = false;
				string filename = resource.ItemSpec;
				string logicalName = resource.GetMetadata("LogicalName");
				if (String.IsNullOrEmpty(logicalName))
					logicalName = Path.GetFileName(resource.ItemSpec);

				IronPython.Hosting.ResourceFile resourceFile = new IronPython.Hosting.ResourceFile(logicalName, filename, publicVisibility);
				resourcesList.Add(resourceFile);
			}
			compiler.ResourceFiles = resourcesList;

			return true;
		}
	}
}
