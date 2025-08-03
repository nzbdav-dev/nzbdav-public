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
        var result = await guessResult(guess);
        if (result == null) return guess;
        
        var newGuess = (int)((guess - startInclusive) * result.Value) + startInclusive;
        if (newGuess >= endExclusive) newGuess = endExclusive - 1;
        if (result < 1 && newGuess >= guess) newGuess--;
        if (result > 1 && newGuess <= guess) newGuess++;
        
        if (newGuess < startInclusive || newGuess >= endExclusive)
            throw new Exception("Could not find through interpolation search.");

        if (newGuess == guess)
            return guess;

        return await Find(newGuess, startInclusive, endExclusive, guessResult);
    }
}