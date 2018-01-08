﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using MLS.Agent.Tools;
using OmniSharp.Client.Commands;
using Pocket;
using WorkspaceServer.Servers.OmniSharp;
using Xunit;
using Xunit.Abstractions;

namespace WorkspaceServer.Tests
{
    public class EmitTests : IDisposable
    {
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public EmitTests(ITestOutputHelper output)
        {
            disposables.Add(output.SubscribeToPocketLogger());
        }

        public void Dispose() => disposables.Dispose();

        [Fact]
        public async Task Console_app_project_can_be_emitted()
        {
            using (var omnisharp = StartOmniSharp())
            {
                await omnisharp.ProjectLoaded();

                var response = await omnisharp.SendCommand<Emit, EmitResponse>(
                                   new Emit(), Default.Timeout());

                File.Exists(response.Body.OutputAssemblyPath).Should().BeTrue();
            }
        }

        [Fact]
        public async Task Emitted_console_app_project_can_be_updated_and_rerun()
        {
            var project = Create.TempProject();

            using (var omnisharp = StartOmniSharp(project.Directory))
            {
                await omnisharp.ProjectLoaded(Default.Timeout());

                var file = await omnisharp.FindFile("Program.cs");

                var code = await file.ReadAsync();

                await omnisharp.UpdateBuffer(
                    file,
                    code.Replace("Hello World", "Hola mundo"));

                var (output, error) = await EmitAndRun(omnisharp);

                output.Should().Contain("Hola mundo!");
            }
        }

        private OmniSharpServer StartOmniSharp(DirectoryInfo projectDirectory = null) =>
            new OmniSharpServer(
                projectDirectory,
                Paths.EmitPlugin,
                logToPocketLogger: true);

        private (IReadOnlyCollection<string>, IReadOnlyCollection<string>) ExecuteEmittedAssembly(string dllPath)
        {
            var result = new Dotnet().Execute(dllPath);

            return (result.Output, result.Error);
        }

        private async Task<(IReadOnlyCollection<string> output, IReadOnlyCollection<string> error)> EmitAndRun(
            OmniSharpServer omnisharp)
        {
            var response = await omnisharp.Emit(Default.Timeout());

            return ExecuteEmittedAssembly(response.Body.OutputAssemblyPath);
        }
    }
}
