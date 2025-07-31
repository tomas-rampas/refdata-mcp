using McpServer.Core.Interfaces;

namespace McpServer.Infrastructure.Services;

/// <summary>
/// Implements sliding window text chunking for preparing documents for vector embedding.
/// Maintains sentence boundaries and configurable overlap to preserve context between chunks.
/// </summary>
public class ChunkingService : IChunkingService
{
    /// <inheritdoc cref="IChunkingService.ChunkDocumentAsync"/>
    public Task<IEnumerable<TextChunk>> ChunkDocumentAsync(string content, int chunkSize = 1000, int overlap = 200, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(content))
        {
            return Task.FromResult(Enumerable.Empty<TextChunk>());
        }

        if (chunkSize <= 0)
            throw new ArgumentException("Chunk size must be positive", nameof(chunkSize));
        
        if (overlap < 0)
            throw new ArgumentException("Overlap size cannot be negative", nameof(overlap));
        
        if (overlap >= chunkSize)
            throw new ArgumentException("Overlap size must be less than chunk size", nameof(overlap));

        var chunks = new List<TextChunk>();
        var sentences = SplitIntoSentences(content);
        var currentChunk = new List<string>();
        var currentLength = 0;
        var currentStartIndex = 0;

        foreach (var sentence in sentences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sentenceLength = sentence.Length;

            // If single sentence exceeds chunk size, split it
            if (sentenceLength > chunkSize)
            {
                // Save current chunk if it has content
                if (currentChunk.Count > 0)
                {
                    var chunkContent = string.Join(" ", currentChunk);
                    chunks.Add(new TextChunk
                    {
                        Content = chunkContent,
                        StartIndex = currentStartIndex,
                        EndIndex = currentStartIndex + chunkContent.Length
                    });
                    currentChunk.Clear();
                    currentLength = 0;
                    currentStartIndex += chunkContent.Length + 1;
                }

                // Split long sentence
                var words = sentence.Split(' ');
                var tempChunk = new List<string>();
                var tempLength = 0;

                foreach (var word in words)
                {
                    if (tempLength + word.Length + 1 > chunkSize)
                    {
                        var wordChunkContent = string.Join(" ", tempChunk);
                        chunks.Add(new TextChunk
                        {
                            Content = wordChunkContent,
                            StartIndex = currentStartIndex,
                            EndIndex = currentStartIndex + wordChunkContent.Length
                        });
                        currentStartIndex += wordChunkContent.Length + 1;
                        tempChunk.Clear();
                        tempLength = 0;
                    }
                    tempChunk.Add(word);
                    tempLength += word.Length + 1;
                }

                if (tempChunk.Count > 0)
                {
                    var wordChunkContent = string.Join(" ", tempChunk);
                    chunks.Add(new TextChunk
                    {
                        Content = wordChunkContent,
                        StartIndex = currentStartIndex,
                        EndIndex = currentStartIndex + wordChunkContent.Length
                    });
                    currentStartIndex += wordChunkContent.Length + 1;
                }
            }
            else if (currentLength + sentenceLength + 1 > chunkSize)
            {
                // Current chunk is full, save it
                var chunkContent = string.Join(" ", currentChunk);
                chunks.Add(new TextChunk
                {
                    Content = chunkContent,
                    StartIndex = currentStartIndex,
                    EndIndex = currentStartIndex + chunkContent.Length
                });

                // Start new chunk with overlap
                currentChunk = GetOverlapSentences(currentChunk, overlap);
                currentLength = currentChunk.Sum(s => s.Length + 1);
                currentStartIndex = currentStartIndex + chunkContent.Length + 1 - currentLength;
                
                currentChunk.Add(sentence);
                currentLength += sentenceLength + 1;
            }
            else
            {
                // Add sentence to current chunk
                currentChunk.Add(sentence);
                currentLength += sentenceLength + 1;
            }
        }

        // Add remaining chunk
        if (currentChunk.Count > 0)
        {
            var chunkContent = string.Join(" ", currentChunk);
            chunks.Add(new TextChunk
            {
                Content = chunkContent,
                StartIndex = currentStartIndex,
                EndIndex = currentStartIndex + chunkContent.Length
            });
        }

        return Task.FromResult(chunks.AsEnumerable());
    }

    private List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitting - in production, use more sophisticated NLP
        var sentences = new List<string>();
        var sentenceEnders = new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" };
        
        var currentSentence = "";
        var chars = text.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            currentSentence += chars[i];

            // Check for sentence end
            bool isSentenceEnd = false;
            foreach (var ender in sentenceEnders)
            {
                if (i + ender.Length <= chars.Length)
                {
                    var substring = text.Substring(i, Math.Min(ender.Length, chars.Length - i));
                    if (substring == ender)
                    {
                        isSentenceEnd = true;
                        break;
                    }
                }
            }

            if (isSentenceEnd || i == chars.Length - 1)
            {
                var trimmed = currentSentence.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    sentences.Add(trimmed);
                }
                currentSentence = "";
            }
        }

        return sentences;
    }

    private List<string> GetOverlapSentences(List<string> sentences, int overlapSize)
    {
        if (overlapSize == 0 || sentences.Count == 0)
            return new List<string>();

        var overlapChars = 0;
        var overlapSentences = new List<string>();

        // Work backwards to get overlap
        for (int i = sentences.Count - 1; i >= 0; i--)
        {
            overlapSentences.Insert(0, sentences[i]);
            overlapChars += sentences[i].Length + 1;

            if (overlapChars >= overlapSize)
                break;
        }

        return overlapSentences;
    }
}