namespace Vicsek.Core;

/// <summary>
/// Vicsek model in a periodic square box.
/// Each step every particle takes the mean heading of the particles within
/// <see cref="SimulationParameters.Radius"/> (itself included), adds uniform noise,
/// and moves <see cref="SimulationParameters.Speed"/> units along the new heading.
/// Neighbours are found with a cell list whose cells are at least one radius wide,
/// so only the 3x3 block of cells around a particle has to be searched.
/// </summary>
public sealed class Simulation
{
    private readonly Particle[] _particles;
    private readonly double[] _nextTheta;
    private readonly int[] _cellOf;
    private readonly int[] _sorted;
    private int[] _cellStart = [];
    private readonly Random _random;

    /// <summary>
    /// Live parameters. <see cref="SimulationParameters.Radius"/>, <see cref="SimulationParameters.Speed"/>
    /// and <see cref="SimulationParameters.Noise"/> may be changed between steps;
    /// <see cref="SimulationParameters.Count"/> and <see cref="SimulationParameters.BoxSize"/> are fixed at construction.
    /// </summary>
    public SimulationParameters Parameters { get; }

    public double BoxSize { get; }
    public int StepCount { get; private set; }
    public int CellsPerSide { get; private set; }
    public double CellSize => BoxSize / CellsPerSide;
    public ReadOnlySpan<Particle> Particles => _particles;

    /// <summary>Creates a simulation with uniformly random positions and headings.</summary>
    public Simulation(SimulationParameters parameters, int? seed = null)
        : this(parameters, null, seed)
    {
    }

    /// <summary>Creates a simulation from explicit initial particles (positions are wrapped into the box).</summary>
    public Simulation(SimulationParameters parameters, IReadOnlyList<Particle>? initial, int? seed = null)
    {
        Parameters = parameters.Clone();
        Parameters.Validate();
        BoxSize = Parameters.BoxSize;
        _random = seed is { } s ? new Random(s) : new Random();

        int count = initial?.Count ?? Parameters.Count;
        Parameters.Count = count;
        _particles = new Particle[count];
        _nextTheta = new double[count];
        _cellOf = new int[count];
        _sorted = new int[count];

        for (int i = 0; i < count; i++)
        {
            var p = initial is null
                ? new Particle(_random.NextDouble() * BoxSize, _random.NextDouble() * BoxSize, _random.NextDouble() * 2 * Math.PI)
                : initial[i];
            _particles[i] = new Particle(Wrap(p.X, BoxSize), Wrap(p.Y, BoxSize), NormalizeAngle(p.Theta));
        }
    }

    /// <summary>Advances the system one time step.</summary>
    public void Step()
    {
        Parameters.Validate();
        BuildCellList(Parameters.Radius);

        double radius2 = Parameters.Radius * Parameters.Radius;
        double halfBox = BoxSize / 2;
        int n = CellsPerSide;
        Span<int> cols = stackalloc int[3];
        Span<int> rows = stackalloc int[3];

        for (int i = 0; i < _particles.Length; i++)
        {
            var pi = _particles[i];
            int cell = _cellOf[i];
            int nCols = NeighbourIndices(cell % n, n, cols);
            int nRows = NeighbourIndices(cell / n, n, rows);

            double sumCos = 0, sumSin = 0;
            for (int r = 0; r < nRows; r++)
            {
                for (int c = 0; c < nCols; c++)
                {
                    int neighbourCell = rows[r] * n + cols[c];
                    for (int k = _cellStart[neighbourCell]; k < _cellStart[neighbourCell + 1]; k++)
                    {
                        var pj = _particles[_sorted[k]];
                        double dx = MinimumImage(pj.X - pi.X, BoxSize, halfBox);
                        double dy = MinimumImage(pj.Y - pi.Y, BoxSize, halfBox);
                        if (dx * dx + dy * dy <= radius2)
                        {
                            sumCos += Math.Cos(pj.Theta);
                            sumSin += Math.Sin(pj.Theta);
                        }
                    }
                }
            }

            double noise = (_random.NextDouble() - 0.5) * Parameters.Noise;
            _nextTheta[i] = Math.Atan2(sumSin, sumCos) + noise;
        }

        double speed = Parameters.Speed;
        for (int i = 0; i < _particles.Length; i++)
        {
            double theta = NormalizeAngle(_nextTheta[i]);
            var p = _particles[i];
            _particles[i] = new Particle(
                Wrap(p.X + speed * Math.Cos(theta), BoxSize),
                Wrap(p.Y + speed * Math.Sin(theta), BoxSize),
                theta);
        }

        StepCount++;
    }

    /// <summary>
    /// Polar order parameter: magnitude of the mean unit velocity, from 0 (disordered) to 1 (all aligned).
    /// </summary>
    public double OrderParameter()
    {
        if (_particles.Length == 0) return 0;
        double sumCos = 0, sumSin = 0;
        foreach (var p in _particles)
        {
            sumCos += Math.Cos(p.Theta);
            sumSin += Math.Sin(p.Theta);
        }
        return Math.Sqrt(sumCos * sumCos + sumSin * sumSin) / _particles.Length;
    }

    private void BuildCellList(double radius)
    {
        int n = Math.Max(1, (int)(BoxSize / radius));
        CellsPerSide = n;
        int cellCount = n * n;
        if (_cellStart.Length != cellCount + 1)
            _cellStart = new int[cellCount + 1];
        else
            Array.Clear(_cellStart);

        double cellSize = BoxSize / n;
        for (int i = 0; i < _particles.Length; i++)
        {
            int cx = Math.Min((int)(_particles[i].X / cellSize), n - 1);
            int cy = Math.Min((int)(_particles[i].Y / cellSize), n - 1);
            int cell = cy * n + cx;
            _cellOf[i] = cell;
            _cellStart[cell + 1]++;
        }

        for (int c = 0; c < cellCount; c++)
            _cellStart[c + 1] += _cellStart[c];

        // Counting sort: _cellStart[c] is used as a write cursor, then shifted back.
        for (int i = 0; i < _particles.Length; i++)
            _sorted[_cellStart[_cellOf[i]]++] = i;
        for (int c = cellCount; c > 0; c--)
            _cellStart[c] = _cellStart[c - 1];
        _cellStart[0] = 0;
    }

    /// <summary>
    /// Indices of the cells adjacent to <paramref name="index"/> along one axis, with periodic wrap.
    /// With fewer than 3 cells per side the offsets -1, 0, +1 would repeat cells, so every cell is listed once instead.
    /// </summary>
    private static int NeighbourIndices(int index, int n, Span<int> result)
    {
        if (n < 3)
        {
            for (int i = 0; i < n; i++) result[i] = i;
            return n;
        }
        result[0] = index == 0 ? n - 1 : index - 1;
        result[1] = index;
        result[2] = index == n - 1 ? 0 : index + 1;
        return 3;
    }

    private static double MinimumImage(double d, double box, double halfBox)
    {
        if (d > halfBox) return d - box;
        if (d < -halfBox) return d + box;
        return d;
    }

    internal static double Wrap(double v, double box)
    {
        v %= box;
        if (v < 0) v += box;
        // -tiny + box can round to exactly box.
        return v >= box ? 0 : v;
    }

    internal static double NormalizeAngle(double theta) => Wrap(theta, 2 * Math.PI);
}
