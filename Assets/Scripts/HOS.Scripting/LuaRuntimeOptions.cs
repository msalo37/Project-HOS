using System;

namespace HOS.Scripting
{
    public sealed class LuaRuntimeOptions
    {
        public LuaRuntimeOptions(
            long instructionsPerSlice = 10000,
            long maximumInstructions = 1000000,
            int maximumOutputCharacters = 65536)
        {
            if (instructionsPerSlice <= 0)
                throw new ArgumentOutOfRangeException(nameof(instructionsPerSlice));
            if (maximumInstructions < instructionsPerSlice)
                throw new ArgumentOutOfRangeException(nameof(maximumInstructions));
            if (maximumOutputCharacters <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumOutputCharacters));

            InstructionsPerSlice = instructionsPerSlice;
            MaximumInstructions = maximumInstructions;
            MaximumOutputCharacters = maximumOutputCharacters;
        }

        public long InstructionsPerSlice { get; }
        public long MaximumInstructions { get; }
        public int MaximumOutputCharacters { get; }
    }
}
