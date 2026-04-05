using System.Text;
using System.Text.Json;

namespace QwenCode.App.Runtime;

internal sealed class StreamingToolCallParser
{
    private readonly Dictionary<int, ToolCallBufferState> _states = [];
    private readonly Dictionary<string, int> _idToIndexMap = new(StringComparer.Ordinal);
    private int _nextAvailableIndex;

    /// <summary>
    /// Executes add chunk
    /// </summary>
    /// <param name="index">The index</param>
    /// <param name="chunk">The chunk</param>
    /// <param name="id">The id</param>
    /// <param name="name">The name</param>
    /// <returns>The resulting tool call parse result</returns>
    public ToolCallParseResult AddChunk(int index, string? chunk, string? id, string? name)
    {
        var actualIndex = ResolveIndex(index, id);
        var state = GetOrCreateState(actualIndex);

        if (!string.IsNullOrWhiteSpace(id))
        {
            state.Id = id;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            state.Name = name;
        }

        if (!string.IsNullOrEmpty(chunk))
        {
            state.Buffer.Append(chunk);
            UpdateState(state, chunk);
        }

        if (state.Buffer.Length == 0)
        {
            return ToolCallParseResult.Incomplete();
        }

        if (TryParseState(state, out var parsedArguments, out var repaired))
        {
            return ToolCallParseResult.FromComplete(parsedArguments!, repaired);
        }

        return ToolCallParseResult.Incomplete();
    }

    /// <summary>
    /// Executes has incomplete tool calls
    /// </summary>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool HasIncompleteToolCalls()
    {
        foreach (var state in _states.Values)
        {
            if (string.IsNullOrWhiteSpace(state.Name) || state.Buffer.Length == 0)
            {
                continue;
            }

            if (state.OpenContainers.Count > 0 || state.InString)
            {
                return true;
            }

            if (!TryParseState(state, out _, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets completed tool calls
    /// </summary>
    /// <returns>The resulting i read only list assistant tool call</returns>
    public IReadOnlyList<AssistantToolCall> GetCompletedToolCalls()
    {
        var results = new List<(int Index, AssistantToolCall ToolCall)>();
        foreach (var pair in _states)
        {
            var state = pair.Value;
            if (string.IsNullOrWhiteSpace(state.Name) || state.Buffer.Length == 0)
            {
                continue;
            }

            if (!TryParseState(state, out var arguments, out _))
            {
                continue;
            }

            results.Add((
                pair.Key,
                new AssistantToolCall
                {
                    Id = string.IsNullOrWhiteSpace(state.Id) ? Guid.NewGuid().ToString("N") : state.Id,
                    ToolName = state.Name,
                    ArgumentsJson = arguments ?? "{}"
                }));
        }

        return results
            .OrderBy(static item => item.Index)
            .Select(static item => item.ToolCall)
            .ToArray();
    }

    private int ResolveIndex(int index, string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            if (_idToIndexMap.TryGetValue(id, out var mappedIndex))
            {
                return mappedIndex;
            }

            if (_states.TryGetValue(index, out var existingState) &&
                existingState.IsComplete &&
                !string.Equals(existingState.Id, id, StringComparison.Ordinal))
            {
                index = FindNextAvailableIndex();
            }

            _idToIndexMap[id] = index;
            return index;
        }

        if (_states.TryGetValue(index, out var currentState) && !currentState.IsComplete)
        {
            return index;
        }

        var incomplete = _states
            .Where(static pair => !pair.Value.IsComplete)
            .OrderByDescending(static pair => pair.Key)
            .FirstOrDefault();

        return incomplete.Value is not null ? incomplete.Key : index;
    }

    private ToolCallBufferState GetOrCreateState(int index)
    {
        if (_states.TryGetValue(index, out var state))
        {
            return state;
        }

        state = new ToolCallBufferState();
        _states[index] = state;
        return state;
    }

    private int FindNextAvailableIndex()
    {
        while (_states.TryGetValue(_nextAvailableIndex, out var state) && state.IsComplete)
        {
            _nextAvailableIndex++;
        }

        return _nextAvailableIndex++;
    }

    private static void UpdateState(ToolCallBufferState state, string chunk)
    {
        foreach (var character in chunk)
        {
            if (state.InString)
            {
                if (state.IsEscaped)
                {
                    state.IsEscaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    state.IsEscaped = true;
                    continue;
                }

                if (character == '"')
                {
                    state.InString = false;
                }

                continue;
            }

            if (character == '"')
            {
                state.InString = true;
                continue;
            }

            switch (character)
            {
                case '{':
                case '[':
                    state.OpenContainers.Push(character);
                    break;
                case '}':
                    if (state.OpenContainers.Count > 0 && state.OpenContainers.Peek() == '{')
                    {
                        state.OpenContainers.Pop();
                    }

                    break;
                case ']':
                    if (state.OpenContainers.Count > 0 && state.OpenContainers.Peek() == '[')
                    {
                        state.OpenContainers.Pop();
                    }

                    break;
            }
        }

        state.IsComplete = state.OpenContainers.Count == 0 &&
                           !state.InString &&
                           TryParseJsonObject(state.Buffer.ToString(), out _);
    }

    private static bool TryParseState(ToolCallBufferState state, out string? argumentsJson, out bool repaired)
    {
        var original = state.Buffer.ToString();
        repaired = false;
        if (TryParseJsonObject(original, out var normalized))
        {
            argumentsJson = normalized;
            return true;
        }

        var repairedCandidate = TryRepairJson(original, state);
        if (!string.Equals(repairedCandidate, original, StringComparison.Ordinal) &&
            TryParseJsonObject(repairedCandidate, out normalized))
        {
            argumentsJson = normalized;
            repaired = true;
            return true;
        }

        argumentsJson = null;
        return false;
    }

    private static bool TryParseJsonObject(string candidate, out string? normalized)
    {
        try
        {
            using var document = JsonDocument.Parse(candidate);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                normalized = null;
                return false;
            }

            normalized = document.RootElement.GetRawText();
            return true;
        }
        catch
        {
            normalized = null;
            return false;
        }
    }

    private static string TryRepairJson(string original, ToolCallBufferState state)
    {
        if (string.IsNullOrWhiteSpace(original))
        {
            return original;
        }

        var repaired = new StringBuilder(original);
        if (state.InString)
        {
            repaired.Append('"');
        }

        if (state.OpenContainers.Count == 0)
        {
            return repaired.ToString();
        }

        foreach (var container in state.OpenContainers)
        {
            repaired.Append(container == '{' ? '}' : ']');
        }

        return repaired.ToString();
    }

    private sealed class ToolCallBufferState
    {
        /// <summary>
        /// Gets or sets the id
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the buffer
        /// </summary>
        public StringBuilder Buffer { get; } = new();

        /// <summary>
        /// Gets the open containers
        /// </summary>
        public Stack<char> OpenContainers { get; } = new();

        /// <summary>
        /// Gets or sets the in string
        /// </summary>
        public bool InString { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is escaped
        /// </summary>
        public bool IsEscaped { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is complete
        /// </summary>
        public bool IsComplete { get; set; }
    }
}

internal readonly record struct ToolCallParseResult(bool Complete, string? Value, bool Repaired)
{
    /// <summary>
    /// Executes incomplete
    /// </summary>
    /// <returns>The resulting tool call parse result</returns>
    public static ToolCallParseResult Incomplete() => new(false, null, false);

    /// <summary>
    /// Executes from complete
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="repaired">The repaired</param>
    /// <returns>The resulting tool call parse result</returns>
    public static ToolCallParseResult FromComplete(string value, bool repaired) => new(true, value, repaired);
}
