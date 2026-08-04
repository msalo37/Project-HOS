using System;
using System.Collections.Generic;
using HOS.Application.Shell;

namespace HOS.Application.Execution
{
    public sealed class BinaryProgramRuntime : IProgramRuntime
    {
        private readonly BuiltinProgramRegistry registry;

        public BinaryProgramRuntime(BuiltinProgramRegistry registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public ProgramStartResult Start(
            ProgramExecutionContext context,
            ProgramImage image,
            IReadOnlyList<string> arguments)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (!HosBinaryFormat.TryParse(image.Content, out var descriptor, out var error))
            {
                return ProgramStartResult.Completed(
                    CommandResult.Failure(
                        126,
                        $"{image.Path}: invalid HOS executable: {error}"));
            }

            if (!registry.TryGet(descriptor.ProgramId, out var program))
            {
                return ProgramStartResult.Completed(
                    CommandResult.Failure(
                        126,
                        $"{image.Path}: HOS program '{descriptor.ProgramId}' is unavailable"));
            }

            return program.Start(context, arguments ?? Array.Empty<string>());
        }
    }
}
