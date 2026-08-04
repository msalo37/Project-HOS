using System;
using System.Collections.Generic;
using HOS.Application.Shell;

namespace HOS.Application.Execution
{
    public sealed class ProgramRuntimeRouter : IProgramRuntime
    {
        private readonly IProgramRuntime luaRuntime;
        private readonly IProgramRuntime binaryRuntime;

        public ProgramRuntimeRouter(
            IProgramRuntime luaRuntime,
            IProgramRuntime binaryRuntime)
        {
            this.luaRuntime = luaRuntime ?? throw new ArgumentNullException(nameof(luaRuntime));
            this.binaryRuntime = binaryRuntime ?? throw new ArgumentNullException(nameof(binaryRuntime));
        }

        public ProgramStartResult Start(
            ProgramExecutionContext context,
            ProgramImage image,
            IReadOnlyList<string> arguments)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            return image.Path.EndsWith(HosBinaryFormat.Extension, StringComparison.OrdinalIgnoreCase)
                ? binaryRuntime.Start(context, image, arguments)
                : luaRuntime.Start(context, image, arguments);
        }
    }
}
