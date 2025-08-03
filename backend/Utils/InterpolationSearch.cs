namespace NzbWebDAV.Utils;

public static class InterpolationSearch
{
    public static async Task<int> Find
    (
        int startInclusive,
        int endExclusive,
        Func<int, Task<double?>> getGuessResult
    )
    {
        var guess = (startInclusive + endExclusive) / 2;
        return await Find(guess, startInclusive, endExclusive, getGuessResult);
    }

    private static async Task<int> Find
    (
        int guess,
        int startInclusive,
        int endExclusive,
        Func<int, Task<double?>> guessResult
    )
    {
        const int maxIterations = 100;
        var visitedGuesses = new HashSet<int>();
        
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            if (visitedGuesses.Contains(guess))
            {
                // If we've seen this guess before, we're in a loop - return it
                return guess;
            }
            
            visitedGuesses.Add(guess);
            
            var result = await guessResult(guess);
            if (result == null) return guess;
            
            var newGuess = (int)((guess - startInclusive) * result.Value) + startInclusive;
            if (newGuess >= endExclusive) newGuess = endExclusive - 1;
            if (newGuess < startInclusive) newGuess = startInclusive;
            
            // Ensure we make progress when result indicates direction
            if (result < 1 && newGuess >= guess) newGuess = Math.Max(startInclusive, guess - 1);
            if (result > 1 && newGuess <= guess) newGuess = Math.Min(endExclusive - 1, guess + 1);
            
            if (newGuess < startInclusive || newGuess >= endExclusive)
                return guess; // Return current guess instead of throwing
            
            if (newGuess == guess)
                return guess;
            
            guess = newGuess;
        }
        
        // If we hit max iterations, return the last guess
        return guess;
    }
}