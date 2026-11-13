namespace CrypTool.JosseCipherAnalyzer.AttackTypes
{
    /// <summary>
    /// Hill climbing with random restarts: replaces one key letter at a time and keeps every
    /// change that improves the rating, until no single change helps any more.
    /// </summary>
    public class Hillclimbing : AttackType
    {
        public Hillclimbing(string alphabet, int keyLength, int restarts)
            : base(alphabet, keyLength, restarts)
        {
        }

        protected override ResultEntry Search(CipherImplementation cipher, int[] ciphertext)
        {
            int alphabetLength = Alphabet.Length;
            int[] plaintext = new int[ciphertext.Length];
            int[] key = new int[KeyLength];
            bool[] used = new bool[alphabetLength];
            ResultEntry best = null;

            for (int restart = 0; restart < Restarts; restart++)
            {
                RandomKey(key, used);
                double cost = Evaluate(cipher, key, ciphertext, plaintext);

                bool improved;
                do
                {
                    improved = false;
                    for (int i = 0; i < KeyLength; i++)
                    {
                        for (int letter = 0; letter < alphabetLength; letter++)
                        {
                            if (used[letter])
                            {
                                continue;
                            }

                            int oldLetter = key[i];
                            key[i] = letter;
                            double newCost = Evaluate(cipher, key, ciphertext, plaintext);
                            if (newCost > cost)
                            {
                                cost = newCost;
                                used[oldLetter] = false;
                                used[letter] = true;
                                improved = true;
                            }
                            else
                            {
                                key[i] = oldLetter;
                            }

                            if (ShouldStop)
                            {
                                return null;
                            }
                        }
                    }
                } while (improved);

                ResultEntry result = CreateResult(cipher, key, cost, ciphertext, plaintext);
                OnResultFound(result);
                if (best == null || result.Value > best.Value)
                {
                    best = result;
                }
                OnProgressChanged((restart + 1.0) / Restarts);
            }

            return best;
        }
    }
}
