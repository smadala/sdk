﻿using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using FluentAssertions;
using System.Runtime.InteropServices;
using System.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToVerifyProjectReferenceCompat : SdkTest
    {
        [Theory]
        [InlineData("net45", "FullMatrix", "netstandard1.0 netstandard1.1 net45", true, true)]
        [InlineData("net451", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 net45 net451", true, true)]
        [InlineData("net46", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 net45 net451 net46", true, true)]
        [InlineData("net461", "PartialM3", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 net45 net451 net46 net461", true, true)]
        [InlineData("net462", "PartialM2", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 net45 net451 net46 net461", true, true)]
        //  Fullframework NuGet versioning on Jenkins infrastructure issue
        //        https://github.com/dotnet/sdk/issues/1041
        //[InlineData("net461", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461", true, true)]
        //[InlineData("net462", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461", true, true)]

        [InlineData("netstandard1.0", "FullMatrix", "netstandard1.0", true, true)]
        [InlineData("netstandard1.1", "FullMatrix", "netstandard1.0 netstandard1.1", true, true)]
        [InlineData("netstandard1.2", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2", true, true)]
        [InlineData("netstandard1.3", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3", true, true)]
        [InlineData("netstandard1.4", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4", true, true)]
        [InlineData("netstandard1.5", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5", true, true)]
        [InlineData("netstandard1.6", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6", true, true)]
        [InlineData("netstandard2.0", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0", true, true)]
        [InlineData("netcoreapp1.0", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0", true, true)]
        [InlineData("netcoreapp1.1", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0 netcoreapp1.1", true, true)]
        [InlineData("netcoreapp2.0", "PartialM1", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0 netcoreapp1.1 netcoreapp2.0", true, true)]
        //  Fullframework NuGet versioning on Jenkins infrastructure issue
        //        https://github.com/dotnet/sdk/issues/1041
        //[InlineData("netcoreapp2.0", "FullMatrix", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 netcoreapp1.0 netcoreapp1.1 netcoreapp2.0", true, true)]

        public void Project_reference_compat(string referencerTarget, string testIDPostFix, string rawDependencyTargets, 
                bool restoreSucceeds, bool buildSucceeds)
        {
            string identifier = "_TestID_" + referencerTarget + "_" + testIDPostFix;

            TestProject referencerProject = GetTestProject("Referencer", referencerTarget, true);
            List<string> dependencyTargets = rawDependencyTargets.Split(',', ';', ' ').ToList();
            int dependencyTargetNamingIndex = 1;
            foreach (string dependencyTarget in dependencyTargets)
            {
                TestProject dependencyProject = GetTestProject("Dependency" + dependencyTargetNamingIndex++, dependencyTarget, true);
                referencerProject.ReferencedProjects.Add(dependencyProject);
            }

            //  Skip running test if not running on Windows
            //        https://github.com/dotnet/sdk/issues/335
            if (!(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || referencerProject.BuildsOnNonWindows))
            {
                return;
            }

            //  Set the referencer project as an Exe unless it targets .NET Standard
            if (!referencerProject.ShortTargetFrameworkIdentifiers.Contains("netstandard"))
            {
                referencerProject.IsExe = true;
            }

            var testAsset = _testAssetsManager.CreateTestProject(referencerProject, nameof(Project_reference_compat), identifier);
            var restoreCommand = testAsset.GetRestoreCommand(relativePath: referencerProject.Name);
            if (restoreSucceeds)
            {
                restoreCommand.Execute().Should().Pass();
            }
            else
            {
                restoreCommand.CaptureStdOut().Execute().Should().Fail();
            }

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, referencerProject.Name);

            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);
            if (!buildSucceeds)
            {
                buildCommand = buildCommand.CaptureStdOut();
            }

            var result = buildCommand.Execute();

            if (buildSucceeds)
            {
                result.Should().Pass();
            }
            else
            {
                result.Should().Fail().And.HaveStdOutContaining("It cannot be referenced by a project that targets");
            }

        }

        TestProject GetTestProject(string name, string target, bool isSdkProject)
        {
            TestProject ret = new TestProject()
            {
                Name = name,
                IsSdkProject = isSdkProject
            };

            if (isSdkProject)
            {
                ret.TargetFrameworks = target;
            }
            else
            {
                ret.TargetFrameworkVersion = target;
            }

            return ret;
        }

        bool AllProjectsBuildOnNonWindows(TestProject referencerProject)
        {
            return (referencerProject.BuildsOnNonWindows && referencerProject.ReferencedProjects.All(rp => rp.BuildsOnNonWindows));
        }

    }
}
