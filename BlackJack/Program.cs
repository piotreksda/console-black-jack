using System;
using System.Collections.Generic;
using System.Linq;

#region Models
enum Suit { Clubs, Diamonds, Hearts, Spades }

record Card(Suit Suit, int PipValue, string RankLabel);

class Deck
{
    private readonly Stack<Card> _cards;
    private static readonly Random _rng = new();

    public Deck(int decks = 6)
    {
        var list = new List<Card>(52 * decks);
        for (int d = 0; d < decks; d++)
        {
            foreach (Suit s in Enum.GetValues(typeof(Suit)))
            {
                for (int pip = 2; pip <= 10; pip++)
                    list.Add(new Card(s, pip, pip.ToString()));
                list.Add(new Card(s, 10, "J"));
                list.Add(new Card(s, 10, "Q"));
                list.Add(new Card(s, 10, "K"));
                list.Add(new Card(s, 11, "A"));
            }
        }
        Shuffle(list);
        _cards = new Stack<Card>(list);
    }

    private static void Shuffle(List<Card> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public Card Draw() => _cards.Pop();
    public int Count => _cards.Count;
}

class Hand
{
    private readonly List<Card> _cards = new();
    public IEnumerable<Card> Cards => _cards;
    public void Add(Card c) => _cards.Add(c);

    public int Score
    {
        get
        {
            int sum = _cards.Sum(c => c.PipValue);
            int aces = _cards.Count(c => c.RankLabel == "A");
            while (sum > 21 && aces > 0)
            {
                sum -= 10;
                aces--;
            }
            return sum;
        }
    }

    public bool IsBlackjack => _cards.Count == 2 && Score == 21;
    public bool IsBust => Score > 21;
}
#endregion

#region ASCII UI
static class AsciiCards
{
    public static string[] RenderCard(Card c)
    {
        string suit = c.Suit switch
        {
            Suit.Clubs => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts => "♥",
            _ => "♠"
        };

        string rank = c.RankLabel;
        string rankPadL = rank.PadRight(2);
        string rankPadR = rank.PadLeft(2);

        return new[]
        {
            "┌─────────┐",
            $"│{rankPadL}       │",
            "│         │",
            $"│    {suit}    │",
            "│         │",
            $"│       {rankPadR}│",
            "└─────────┘"
        };
    }

    public static string[] RenderHidden()
    {
        return new[]
        {
            "┌─────────┐",
            "│░░░░░░░░░│",
            "│░░░░░░░░░│",
            "│░░░░░░░░░│",
            "│░░░░░░░░░│",
            "│░░░░░░░░░│",
            "└─────────┘"
        };
    }

    public static void PrintHand(IEnumerable<Card> cards, bool hideFirst = false)
    {
        var rendered = new List<string[]>();
        bool first = true;
        foreach (var c in cards)
        {
            if (hideFirst && first)
                rendered.Add(RenderHidden());
            else
                rendered.Add(RenderCard(c));
            first = false;
        }

        for (int i = 0; i < 7; i++)
        {
            foreach (var r in rendered)
                Console.Write(r[i] + " ");
            Console.WriteLine();
        }
    }
}
#endregion

#region Game
class BlackjackGame
{
    private Deck _deck;
    private decimal _bankroll;

    public BlackjackGame(decimal startingBankroll = 200m, int decks = 6)
    {
        _bankroll = startingBankroll;
        _deck = new Deck(decks);
    }

    public void Run()
    {
        Console.WriteLine("=== BLACKJACK ===");
        Console.WriteLine("Zasady: BJ=3:2, krupier stoi na 17 (w tym soft).");
        Console.WriteLine("Komendy: [H]it = dobierz, [S]tand = stój, [D]ouble = podwój (pierwszy ruch), [Q]uit = wyjdź");

        while (_bankroll > 0)
        {
            if (_deck.Count < 52) _deck = new Deck();

            decimal bet = AskBet();
            if (bet == 0) break;

            var player = new Hand();
            var dealer = new Hand();

            player.Add(_deck.Draw());
            dealer.Add(_deck.Draw());
            player.Add(_deck.Draw());
            dealer.Add(_deck.Draw());

            Console.Clear();
            Console.WriteLine("DEALER:");
            AsciiCards.PrintHand(dealer.Cards, hideFirst: true);
            Console.WriteLine();
            Console.WriteLine("PLAYER:");
            AsciiCards.PrintHand(player.Cards);
            Console.WriteLine($"Score: {player.Score}");

            bool playerTurnOver = false;
            bool canDouble = true;

            if (player.IsBlackjack || dealer.IsBlackjack)
            {
                RevealDealer(dealer);
                SettleNatural(player, dealer, bet, ref _bankroll);
                if (!AskPlayAgain()) break;
                continue;
            }

            while (!playerTurnOver)
            {
                Console.Write("\nTwój ruch ([H]it/[S]tand" + (canDouble ? "/[D]ouble" : "") + "): ");
                var key = Console.ReadKey(true).KeyChar;
                Console.WriteLine();

                switch (char.ToLowerInvariant(key))
                {
                    case 'h':
                        player.Add(_deck.Draw());
                        ShowTable(player, dealer, hideDealer: true);
                        canDouble = false;
                        if (player.IsBust)
                        {
                            Console.WriteLine($"Bust! {player.Score}");
                            _bankroll -= bet;
                            playerTurnOver = true;
                        }
                        break;

                    case 's':
                        playerTurnOver = true;
                        break;

                    case 'd' when canDouble && _bankroll >= bet:
                        _bankroll -= bet;
                        bet *= 2;
                        player.Add(_deck.Draw());
                        ShowTable(player, dealer, hideDealer: true);
                        if (player.IsBust)
                        {
                            Console.WriteLine($"Bust after double! {player.Score}");
                            playerTurnOver = true;
                        }
                        playerTurnOver = true;
                        break;

                    case 'q':
                        return;
                }
            }

            if (!player.IsBust)
            {
                RevealDealer(dealer);
                while (dealer.Score < 17)
                {
                    dealer.Add(_deck.Draw());
                    RevealDealer(dealer);
                }

                Settle(player, dealer, bet, ref _bankroll);
            }

            if (_bankroll <= 0)
            {
                Console.WriteLine("Bankroll wyczerpany.");
                break;
            }

            if (!AskPlayAgain()) break;
        }
    }

    private void ShowTable(Hand player, Hand dealer, bool hideDealer)
    {
        Console.Clear();
        Console.WriteLine("DEALER:");
        AsciiCards.PrintHand(dealer.Cards, hideDealer);
        Console.WriteLine();
        Console.WriteLine("PLAYER:");
        AsciiCards.PrintHand(player.Cards);
        Console.WriteLine($"Score: {player.Score}");
    }

    private void RevealDealer(Hand dealer)
    {
        Console.WriteLine("\nDEALER odsłania:");
        AsciiCards.PrintHand(dealer.Cards);
        Console.WriteLine($"Dealer score: {dealer.Score}");
    }

    private void SettleNatural(Hand player, Hand dealer, decimal bet, ref decimal bankroll)
    {
        if (player.IsBlackjack && dealer.IsBlackjack)
        {
            Console.WriteLine("Obie strony BJ – push.");
        }
        else if (player.IsBlackjack)
        {
            var win = bet * 1.5m;
            bankroll += win;
            Console.WriteLine($"BLACKJACK! +{win:C}");
        }
        else if (dealer.IsBlackjack)
        {
            bankroll -= bet;
            Console.WriteLine($"Dealer ma BJ. -{bet:C}");
        }
        Console.WriteLine($"Bankroll: {bankroll:C}");
    }

    private void Settle(Hand player, Hand dealer, decimal bet, ref decimal bankroll)
    {
        int ps = player.Score;
        int ds = dealer.Score;

        if (dealer.IsBust)
        {
            bankroll += bet;
            Console.WriteLine($"Dealer bust ({ds}). +{bet:C}");
        }
        else if (ps > ds)
        {
            bankroll += bet;
            Console.WriteLine($"Wygrana {ps} vs {ds}. +{bet:C}");
        }
        else if (ps < ds)
        {
            bankroll -= bet;
            Console.WriteLine($"Przegrana {ps} vs {ds}. -{bet:C}");
        }
        else
        {
            Console.WriteLine($"Push {ps} vs {ds}.");
        }
        Console.WriteLine($"Bankroll: {bankroll:C}");
    }

    private decimal AskBet()
    {
        Console.WriteLine($"\nTwój bankroll: {_bankroll:C}");
        while (true)
        {
            Console.Write("Stawka (ENTER=wyjście): ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) return 0;
            if (decimal.TryParse(input, out var bet) && bet > 0 && bet <= _bankroll)
                return bet;
            Console.WriteLine("Nieprawidłowa stawka.");
        }
    }

    private static bool AskPlayAgain()
    {
        Console.Write("Gramy dalej? [T/n]: ");
        var k = Console.ReadKey(true).KeyChar;
        Console.WriteLine();
        return !(k == 'n' || k == 'N');
    }
}
#endregion

class Program
{
    static void Main()
    {
        while (true)
        {
            new BlackjackGame().Run();
            Console.Clear();
        }
    }
}