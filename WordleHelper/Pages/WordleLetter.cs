namespace WordleHelper.Pages
{ 

    public enum WordleLetterStatus
    {
        NotPresent,
        PresentButWrongLocation,
        PresentAndRightLocation
    }

    public enum WordleRestrictionType
    {
        ExactOccurances,
        MinOccurances,
        MustOccurInPosition,
        MustNotOccurInPosition
    }
    public record class WordleRestriction
    {
        public Char letter { get; set; }
        public int value { get; set; }
        public WordleRestrictionType type {get; set;}
    }

    public record class WordleWord
    {
        public WordleWord(string word)
        {
            if (word.Length != numLetters)
            {
                throw new Exception("Wrong word length");
            }
            Word = word.ToLower();
        }
        public const int numLetters = 5;
        public string Word { get; set; }

        public int NumOccurances(char letter)
        {
            return Word.Where(x => x == letter).Count();
        }
        public bool HasMinOccurances(char letter, int num)
        {
            return NumOccurances(letter) >= num;
        }
        public bool HasExactOccurances(char letter, int num)
        {
            return NumOccurances(letter) == num;
        }

        public bool HasAtPosition(char letter, int pos)
        {
            return Word[pos] == letter;
        }
        public bool FitsRestriction(WordleRestriction r)
        {
            switch(r.type)
            {
                case WordleRestrictionType.MinOccurances:
                    return HasMinOccurances(r.letter, r.value);
                case WordleRestrictionType.ExactOccurances:
                    return HasExactOccurances(r.letter, r.value);
                case WordleRestrictionType.MustNotOccurInPosition:
                    return !HasAtPosition(r.letter, r.value);
                case WordleRestrictionType.MustOccurInPosition:
                    return HasAtPosition(r.letter, r.value);
            }
            return false;
        }

        public bool FitsRestrictions(IEnumerable<WordleRestriction> rs)
        {
            return rs.All(x => FitsRestriction(x));
        }
    }
    public class WordleLetter
    {
        public int pos { get; set; }
        public Char letter { get; set; }
        public WordleLetterStatus status { get; set; } = WordleLetterStatus.NotPresent;
    }

    public class WordleGame
    {
        public const int numLines = 6;

        public WordleGame()
        {
            for (int i = 0; i < numLines; i++)
            {
                lines[i] = new WordleLine();
                lines[i].pos = i;
            }

            // Temporary
            //SetWordList(new List<string>() { "hello", "house", "whooy", "lemon" });
            //lines[0].letters[0].letter = 'h';
            //lines[0].letters[0].status = WordleLetterStatus.PresentAndRightLocation;
        }

        public WordleLine[] lines { get; set; } = new WordleLine[numLines];
        public List<WordleWord> AllWords { get; set; } = new List<WordleWord>();
        public List<string> CandidateWords { get; private set; } = new List<string>();
  
        public void SetLetter(int line0idx, int letter0idx, char c)
        {
            lines[line0idx].letters[letter0idx].letter = c;
        }

        public void SetStatus(int line0idx, int letter0idx, WordleLetterStatus s)
        {
            lines[line0idx].letters[letter0idx].status = s;
        }

        public void UpdateCandidates()
        {
            var restrictions = getRestrictions();

            CandidateWords.Clear();
            
            foreach (var candidate in AllWords)
            {
                if (candidate.FitsRestrictions(restrictions))
                {
                    CandidateWords.Add(candidate.Word);
                }
            }

            if (CandidateWords.Count == 0)
            {
                CandidateWords.Add("No matches!");
            }
        }
        private List<WordleRestriction> getRestrictions()
        {
            // Opportunity to optimize restrictions here
            //  if there is an exact and min, discard the min
            //  combine all in position
            //  combine all not in position (by letter or position?)
            return lines.SelectMany(x => x.getRestrictions()).Distinct().ToList();

        }

        public void SetWordList(IEnumerable<string> words)
        {
            AllWords.Clear();
            foreach (var word in words)
            {
                AllWords.Add(new WordleWord(word));
            }
        }
    }

    public class WordleLine
    {
        public const int numLetters = 5;
        public int pos { get; set; }
        public WordleLine()
        {
            for (int i = 0; i < numLetters; i++)
            {
                letters[i] = new WordleLetter();
                letters[i].pos = i;
            }
        }
        public WordleLetter[] letters { get; set; } = new WordleLetter[numLetters];

        public IEnumerable<WordleRestriction> getRestrictions()
        {
            var r = letters.Where(x => char.IsLetter(x.letter)).GroupBy(x => char.ToLower(x.letter)).ToList();

            foreach (var letterGroup in r)
            {
                var thoseNotPresent = letterGroup.Where(x => x.status == WordleLetterStatus.NotPresent).ToList();
                var thoseWrongLocation = letterGroup.Where(x => x.status == WordleLetterStatus.PresentButWrongLocation).ToList();
                var thoseRightLocation = letterGroup.Where(x => x.status == WordleLetterStatus.PresentAndRightLocation).ToList();

                var numRightOrWrongLocation = thoseWrongLocation.Count + thoseRightLocation.Count;

                if (thoseNotPresent.Count > 0)
                {
                    // If a letter isn't present at all in a word, it will be gray.
                    // However we have to account for the case where the same letter is guessed more than once.
                    // In that case, we only want to put a max value if we got a gray; that says there are no more.
                    //   The number of non-gray is our exact value.
                    yield return new WordleRestriction() { letter = letterGroup.Key, type = WordleRestrictionType.ExactOccurances, value = numRightOrWrongLocation };
                }
                else if (numRightOrWrongLocation > 0)
                {
                    // The number of green and orange tell us at least how many letters there are
                    // If we had any gray, then we know exactly how many there are and already made a restriction for it above.
                    yield return new WordleRestriction() { letter = letterGroup.Key, type = WordleRestrictionType.MinOccurances, value = numRightOrWrongLocation };
                }

                // In the case where we have a letter both gray and orange, all of them are telling us where the letter isn't.
                // Check for orange being non-zero here because otherwise it's just checking gray and that overspecifies (but isn't wrong.. we'd say occurs 0 times but also doesn't occur in this position)
                if (thoseWrongLocation.Count > 0)
                {
                    foreach (var letter in thoseWrongLocation.Concat(thoseNotPresent))
                    {
                        yield return new WordleRestriction() { letter = letterGroup.Key, type = WordleRestrictionType.MustNotOccurInPosition, value = letter.pos };
                    }
                }

                // Green means we know where it goes
                foreach (var letter in thoseRightLocation)
                {
                    yield return new WordleRestriction() { letter = letterGroup.Key, type = WordleRestrictionType.MustOccurInPosition, value = letter.pos };
                }
            }
        }
    }
}
