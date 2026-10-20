using Vicsek.Core;

namespace Vicsek.Tests;

public class SimulationTests
{
    private static SimulationParameters NoiselessParams(double radius = 10, double speed = 0) => new()
    {
        BoxSize = 100,
        Radius = radius,
        Speed = speed,
        Noise = 0,
    };

    private static double AngleDistance(double a, double b)
    {
        double d = Math.Abs(a - b) % (2 * Math.PI);
        return Math.Min(d, 2 * Math.PI - d);
    }

    [Fact]
    public void MeanHeading_AcrossZero_IsNotFlipped()
    {
        double oneDegree = Math.PI / 180;
        var sim = new Simulation(NoiselessParams(), [
            new Particle(50, 50, 2 * Math.PI - oneDegree),
            new Particle(52, 50, oneDegree),
        ], seed: 1);

        sim.Step();

        Assert.All(sim.Particles.ToArray(), p => Assert.True(AngleDistance(p.Theta, 0) < 1e-9, $"theta = {p.Theta}"));
    }

    [Fact]
    public void Neighbours_InAdjacentCells_Interact()
    {
        // Radius 10 in a 100 box gives cells of width 10; x = 9.5 and x = 10.5 fall in different cells.
        var sim = new Simulation(NoiselessParams(), [
            new Particle(9.5, 50, 0),
            new Particle(10.5, 50, Math.PI / 2),
        ], seed: 1);

        sim.Step();

        Assert.All(sim.Particles.ToArray(), p => Assert.Equal(Math.PI / 4, p.Theta, 9));
    }

    [Fact]
    public void Neighbours_AcrossPeriodicBoundary_Interact()
    {
        var sim = new Simulation(NoiselessParams(), [
            new Particle(1, 1, 0),
            new Particle(99, 99, Math.PI / 2),
        ], seed: 1);

        sim.Step();

        Assert.All(sim.Particles.ToArray(), p => Assert.Equal(Math.PI / 4, p.Theta, 9));
    }

    [Fact]
    public void Particles_BeyondRadius_DoNotInteract()
    {
        var sim = new Simulation(NoiselessParams(), [
            new Particle(10, 50, 0),
            new Particle(20.5, 50, Math.PI / 2),
        ], seed: 1);

        sim.Step();

        Assert.Equal(0, sim.Particles[0].Theta, 9);
        Assert.Equal(Math.PI / 2, sim.Particles[1].Theta, 9);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(33)]   // 3 cells per side
    [InlineData(50)]   // 2 cells per side: neighbour offsets would repeat cells
    public void CellList_MatchesBruteForce(double radius)
    {
        var parameters = new SimulationParameters { Count = 500, BoxSize = 100, Radius = radius, Speed = 0, Noise = 0 };
        var sim = new Simulation(parameters, seed: 42);
        var before = sim.Particles.ToArray();

        sim.Step();

        for (int i = 0; i < before.Length; i++)
        {
            double sumCos = 0, sumSin = 0;
            foreach (var pj in before)
            {
                double dx = pj.X - before[i].X, dy = pj.Y - before[i].Y;
                dx -= 100 * Math.Round(dx / 100);
                dy -= 100 * Math.Round(dy / 100);
                if (dx * dx + dy * dy <= radius * radius)
                {
                    sumCos += Math.Cos(pj.Theta);
                    sumSin += Math.Sin(pj.Theta);
                }
            }
            double expected = Math.Atan2(sumSin, sumCos);
            Assert.True(AngleDistance(sim.Particles[i].Theta, expected) < 1e-9, $"particle {i}");
        }
    }

    [Fact]
    public void Positions_StayInsideBox()
    {
        var parameters = new SimulationParameters { Count = 300, BoxSize = 50, Radius = 5, Speed = 7.3, Noise = 2 };
        var sim = new Simulation(parameters, seed: 7);

        for (int s = 0; s < 200; s++)
        {
            sim.Step();
            foreach (var p in sim.Particles)
            {
                Assert.InRange(p.X, 0, 50 - double.Epsilon);
                Assert.InRange(p.Y, 0, 50 - double.Epsilon);
                Assert.InRange(p.Theta, 0, 2 * Math.PI - double.Epsilon);
            }
        }
    }

    [Fact]
    public void Particles_MoveSpeedAlongHeading()
    {
        var sim = new Simulation(NoiselessParams(speed: 2), [new Particle(50, 50, Math.PI / 2)], seed: 1);

        sim.Step();

        Assert.Equal(50, sim.Particles[0].X, 9);
        Assert.Equal(52, sim.Particles[0].Y, 9);
    }

    [Fact]
    public void OrderParameter_IsOneWhenAligned_AndSmallWhenRandom()
    {
        var aligned = new Simulation(NoiselessParams(), Enumerable.Range(0, 100).Select(i => new Particle(i, i, 1.0)).ToList());
        var random = new Simulation(new SimulationParameters { Count = 10000 }, seed: 3);

        Assert.Equal(1, aligned.OrderParameter(), 9);
        Assert.True(random.OrderParameter() < 0.05);
    }

    [Fact]
    public void LowNoise_OrdersTheSystem()
    {
        var parameters = new SimulationParameters { Count = 400, BoxSize = 20, Radius = 1, Speed = 0.1, Noise = 0.3 };
        var sim = new Simulation(parameters, seed: 5);

        for (int s = 0; s < 1000; s++) sim.Step();

        Assert.True(sim.OrderParameter() > 0.8, $"phi = {sim.OrderParameter()}");
    }

    [Fact]
    public void Wrap_HandlesNegativeRoundingToBox()
    {
        Assert.Equal(0, Simulation.Wrap(-1e-17, 100));
        Assert.Equal(99, Simulation.Wrap(-1, 100), 9);
        Assert.Equal(1, Simulation.Wrap(101, 100), 9);
    }
}
