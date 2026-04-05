namespace QwenCode.Tests.Sessions;

public sealed class InterruptedTurnStoreTests
{
    [Fact]
    public void ListRecoverableTurns_AppendsInterruptedMarkerOnlyOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-interrupted-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var chatsDirectory = Path.Combine(root, "chats");
            Directory.CreateDirectory(chatsDirectory);

            var transcriptPath = Path.Combine(chatsDirectory, "recoverable-session.jsonl");
            File.WriteAllText(
                transcriptPath,
                """
                {"uuid":"u-1","parentUuid":null,"sessionId":"recoverable-session","timestamp":"2026-04-02T10:00:00Z","type":"user","cwd":"E:\\workspace","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Recover this turn."}]}}
                """ + Environment.NewLine);

            var store = new InterruptedTurnStore();
            store.Upsert(new ActiveTurnState
            {
                SessionId = "recoverable-session",
                Prompt = "Recover this turn.",
                TranscriptPath = transcriptPath,
                WorkingDirectory = "E:\\workspace",
                GitBranch = "main",
                ToolName = "write_file",
                Stage = "response-delta",
                Status = "streaming",
                ContentSnapshot = "Partial assistant output.",
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                LastUpdatedAtUtc = DateTime.UtcNow
            });

            var firstListing = store.ListRecoverableTurns(chatsDirectory);
            var recoverable = Assert.Single(firstListing);
            Assert.Equal("recoverable-session", recoverable.SessionId);
            Assert.Equal("write_file", recoverable.ToolName);

            var transcriptAfterFirstListing = File.ReadAllLines(transcriptPath);
            Assert.Equal(2, transcriptAfterFirstListing.Length);
            Assert.Contains("\"type\":\"system\"", transcriptAfterFirstListing[1]);
            Assert.Contains("Partial assistant output.", transcriptAfterFirstListing[1]);

            var secondListing = store.ListRecoverableTurns(chatsDirectory);
            Assert.Single(secondListing);

            var transcriptAfterSecondListing = File.ReadAllLines(transcriptPath);
            Assert.Equal(2, transcriptAfterSecondListing.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
