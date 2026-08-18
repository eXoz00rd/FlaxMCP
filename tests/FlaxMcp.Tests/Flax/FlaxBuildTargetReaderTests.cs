using System.Text;
using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxBuildTargetReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    public FlaxBuildTargetReaderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Read_ParsesRealisticGameTargetFile()
    {
        var targetFile = Path.Combine(_tempDir, "GameTarget.Build.cs");
        File.WriteAllText(
            targetFile,
            """
            using Flax.Build;

            public class GameTarget : GameProjectTarget
            {
                /// <inheritdoc />
                public override void Init()
                {
                    base.Init();

                    // Reference the modules for game
                    Modules.Add("Game");
                }
            }
            """
        );

        var info = FlaxBuildTargetReader.Read(targetFile);

        Assert.Equal("GameTarget", info.Name);
        Assert.Equal("GameProjectTarget", info.BaseClass);
        Assert.Equal(["Game"], info.Modules);
    }

    [Fact]
    public void Read_HandlesUtf16EncodedFileWithBom()
    {
        var targetFile = Path.Combine(_tempDir, "GameEditorTarget.Build.cs");
        File.WriteAllText(
            targetFile,
            """
            using Flax.Build;

            public class GameEditorTarget : GameProjectEditorTarget
            {
                public override void Init()
                {
                    base.Init();
                    Modules.Add("Game");
                    Modules.Add("GameEditor");
                }
            }
            """,
            Encoding.Unicode
        );

        var info = FlaxBuildTargetReader.Read(targetFile);

        Assert.Equal("GameEditorTarget", info.Name);
        Assert.Equal("GameProjectEditorTarget", info.BaseClass);
        Assert.Equal(["Game", "GameEditor"], info.Modules);
    }

    [Fact]
    public void Read_WithMissingFile_Throws()
    {
        var missingFile = Path.Combine(_tempDir, "Missing.Build.cs");

        Assert.Throws<InvalidOperationException>(() => FlaxBuildTargetReader.Read(missingFile));
    }
}
