internal sealed class BreakCycle
{
    private long deadline;

    public BreakCycle(long now, bool demo)
    {
        OnBreak = demo;
        deadline = now + (demo ? 20000 : 2400000);
    }

    public bool OnBreak { get; private set; }

    public bool Advance(long now)
    {
        if (now < deadline) return false;
        OnBreak = !OnBreak;
        deadline = now + (OnBreak ? 1200000 : 2400000);
        return true;
    }

    public int RemainingSeconds(long now)
    {
        long remaining = deadline - now;
        return remaining > 0 ? (int)((remaining + 999) / 1000) : 0;
    }
}
