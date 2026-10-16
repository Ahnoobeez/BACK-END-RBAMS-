using System.Security.Cryptography;
using System.Text;

namespace APINIMIRHEMAA.Services
{
    public sealed record SecurityQuestion(int Id, string Text);

    public static class SecurityQuestionCatalog
    {
        public static IReadOnlyList<SecurityQuestion> All { get; } = new[]
        {
            new SecurityQuestion(1, "What is your mother's maiden name?"),
            new SecurityQuestion(2, "What is the first name of your oldest sibling?"),
            new SecurityQuestion(3, "What city were you born in?"),
            new SecurityQuestion(4, "What was the name of your first school?"),
            new SecurityQuestion(5, "What was the name of your childhood best friend?"),
            new SecurityQuestion(6, "What was the name of your first pet?"),
            new SecurityQuestion(7, "What was your childhood nickname?"),
            new SecurityQuestion(8, "What is the name of the street where you grew up?"),
            new SecurityQuestion(9, "What is the middle name of your oldest sibling?"),
            new SecurityQuestion(10, "What was your favorite childhood subject in school?"),
            new SecurityQuestion(11, "What was the name of your first teacher?"),
            new SecurityQuestion(12, "What was the first concert or live event you attended?"),
            new SecurityQuestion(13, "What was the make of your family's first car?"),
            new SecurityQuestion(14, "What was your favorite childhood cartoon?"),
            new SecurityQuestion(15, "What was the name of your childhood neighborhood?"),
            new SecurityQuestion(16, "What was the first book you remember reading?"),
            new SecurityQuestion(17, "What was your favorite childhood game?"),
            new SecurityQuestion(18, "What was the name of your first workplace?"),
            new SecurityQuestion(19, "What was the first job you wanted as a child?"),
            new SecurityQuestion(20, "What was the name of your favorite childhood teacher?"),
            new SecurityQuestion(21, "What was the first mobile phone brand you owned?"),
            new SecurityQuestion(22, "What was the first movie you watched in a cinema?"),
            new SecurityQuestion(23, "What was the name of your first team or club?"),
            new SecurityQuestion(24, "What was your favorite food as a child?"),
            new SecurityQuestion(25, "What was the first country you visited outside your home country?"),
            new SecurityQuestion(26, "What was the name of the first company you worked for?"),
            new SecurityQuestion(27, "What was your favorite childhood vacation destination?"),
            new SecurityQuestion(28, "What was the name of the first friend you made at school?"),
            new SecurityQuestion(29, "What was the first musical instrument you learned to play?"),
            new SecurityQuestion(30, "What was the name of the first organization you joined?")
        };

        public static IReadOnlyList<SecurityQuestion> SelectRandomThree()
        {
            var ids = Enumerable.Range(1, All.Count).ToArray();
            for (var index = ids.Length - 1; index > 0; index--)
            {
                var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
                (ids[index], ids[swapIndex]) = (ids[swapIndex], ids[index]);
            }
            return ids.Take(3).Select(id => All[id - 1]).ToArray();
        }

        public static string NormalizeAnswer(string answer) =>
            answer.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
