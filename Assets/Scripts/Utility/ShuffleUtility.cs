// Utilities/ShuffleUtility.cs
using System.Collections.Generic;

public static class ShuffleUtility
{
    public static List<T> Shuffle<T>(List<T> input)
    {
        List<T> list = new List<T>(input); // copy original
        System.Random rng = new System.Random();

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
    private static System.Random rng = new System.Random();

    public static void Shuffle<T>(T[] array)
    {
        int n = array.Length;
        while (n > 1)
        {
            int k = rng.Next(n--); // ambil random index
            T temp = array[n];
            array[n] = array[k];
            array[k] = temp;
        }
    }

}
