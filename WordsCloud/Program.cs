using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordsCloud
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] lot = new string[2];
            lot[0] = "pierwszy tekst";
            lot[1] = "drugi tekst";

            var wsResults = getScoringWords(lot);

            foreach(WordScore wsResult in wsResults)
            {
                Console.WriteLine("Word = " + wsResult.text);
            }

            Console.ReadLine();
        }

        public static List<WordScore> getScoringWords(string[] listOfText)
        {
            var ws = new List<WordScore>();

            for(int i=0; i<listOfText.Length; i++)
            {
                for (int j = 0; j < listOfText[i].Split(' ').Length; j++)
                    ws.Add(new WordScore(listOfText[i].Split(' ')[j], 0));
                    /* tutaj pod drugi parametr napisac funkcje ktora oblicza ilosc wystapien slowa w juz istniejacej bazie
                        - funkcja docelowo przyjmuje parametr podobienstwa (algorytm lavensteina)
                    */
            }
            return ws;
        }
    }

    public class WordScore
    {
        public string text;
        public int score;

        public WordScore(string t, int s)
        {
            text = t;
            score = s;
        }
    }
}
