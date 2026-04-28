namespace PokerScreenScraper;

internal static class PokerOddsCalculator
{
    private const int Simulaciones = 12000;

    public static OddsResult Calcular(string cartasJugadorTexto, string cartasMesaTexto, int rivales)
    {
        var cartasJugador = ParsearCartas(cartasJugadorTexto).ToList();
        var cartasMesa = ParsearCartas(cartasMesaTexto).ToList();

        if (cartasJugador.Count != 2)
        {
            throw new ArgumentException("Escribe exactamente 2 cartas tuyas. Ejemplo: Ah Kh");
        }

        if (cartasMesa.Count > 5)
        {
            throw new ArgumentException("La mesa no puede tener mas de 5 cartas.");
        }

        var conocidas = cartasJugador.Concat(cartasMesa).ToList();
        if (conocidas.Distinct().Count() != conocidas.Count)
        {
            throw new ArgumentException("Hay cartas repetidas. Revisa tus cartas y la mesa.");
        }

        var barajaBase = CrearBaraja().Except(conocidas).ToList();
        var ganadas = 0;
        var empatadas = 0;
        var perdidas = 0;
        var random = new Random();

        for (var i = 0; i < Simulaciones; i++)
        {
            var baraja = barajaBase.ToList();
            Mezclar(baraja, random);

            var mesa = cartasMesa.ToList();
            while (mesa.Count < 5)
            {
                mesa.Add(Robar(baraja));
            }

            var manoJugador = Evaluar(cartasJugador.Concat(mesa));
            var mejorRival = default(HandValue);
            for (var rival = 0; rival < rivales; rival++)
            {
                var cartasRival = new[] { Robar(baraja), Robar(baraja) };
                var manoRival = Evaluar(cartasRival.Concat(mesa));
                var comparacion = manoRival.CompareTo(mejorRival);

                if (rival == 0 || comparacion > 0)
                {
                    mejorRival = manoRival;
                }
            }

            var contraMejor = manoJugador.CompareTo(mejorRival);
            if (contraMejor > 0)
            {
                ganadas++;
            }
            else if (contraMejor == 0)
            {
                empatadas++;
            }
            else
            {
                perdidas++;
            }
        }

        var manoActual = cartasMesa.Count >= 3
            ? Evaluar(cartasJugador.Concat(cartasMesa))
            : Evaluar(cartasJugador);

        var total = ganadas + empatadas + perdidas;
        return new OddsResult(
            total == 0 ? 0 : (double)ganadas / total,
            total == 0 ? 0 : (double)empatadas / total,
            Describir(manoActual.Category));
    }

    private static IEnumerable<Card> ParsearCartas(string texto)
    {
        var partes = texto.Split(new[] { ' ', ',', ';', '-', '/', '|' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var parteOriginal in partes)
        {
            var parte = parteOriginal.Trim();
            if (parte.Length < 2 || parte.Length > 3)
            {
                throw new ArgumentException($"Carta no valida: {parteOriginal}");
            }

            var paloTexto = char.ToUpperInvariant(parte[^1]);
            var valorTexto = parte[..^1].ToUpperInvariant();

            var valor = valorTexto switch
            {
                "2" => 2,
                "3" => 3,
                "4" => 4,
                "5" => 5,
                "6" => 6,
                "7" => 7,
                "8" => 8,
                "9" => 9,
                "10" or "T" => 10,
                "J" => 11,
                "Q" => 12,
                "K" => 13,
                "A" => 14,
                _ => throw new ArgumentException($"Valor no valido: {parteOriginal}")
            };

            var suit = paloTexto switch
            {
                'H' => Suit.Hearts,
                'D' => Suit.Diamonds,
                'S' or 'P' => Suit.Spades,
                'C' or 'T' => Suit.Clubs,
                _ => throw new ArgumentException($"Palo no valido: {parteOriginal}. Usa H, D, S/P o C/T.")
            };

            yield return new Card(valor, suit);
        }
    }

    private static List<Card> CrearBaraja()
    {
        var baraja = new List<Card>(52);
        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            for (var value = 2; value <= 14; value++)
            {
                baraja.Add(new Card(value, suit));
            }
        }

        return baraja;
    }

    private static void Mezclar(List<Card> cartas, Random random)
    {
        for (var i = cartas.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (cartas[i], cartas[j]) = (cartas[j], cartas[i]);
        }
    }

    private static Card Robar(List<Card> baraja)
    {
        var carta = baraja[^1];
        baraja.RemoveAt(baraja.Count - 1);
        return carta;
    }

    private static HandValue Evaluar(IEnumerable<Card> cartas)
    {
        var lista = cartas.ToList();
        if (lista.Count < 2)
        {
            return default;
        }

        if (lista.Count < 5)
        {
            var valores = lista.Select(carta => carta.Value).OrderDescending().ToArray();
            return new HandValue(0, valores);
        }

        var mejor = default(HandValue);
        for (var a = 0; a < lista.Count - 4; a++)
        for (var b = a + 1; b < lista.Count - 3; b++)
        for (var c = b + 1; c < lista.Count - 2; c++)
        for (var d = c + 1; d < lista.Count - 1; d++)
        for (var e = d + 1; e < lista.Count; e++)
        {
            var mano = EvaluarCinco(new[] { lista[a], lista[b], lista[c], lista[d], lista[e] });
            if (mano.CompareTo(mejor) > 0)
            {
                mejor = mano;
            }
        }

        return mejor;
    }

    private static HandValue EvaluarCinco(IReadOnlyCollection<Card> cartas)
    {
        var valores = cartas.Select(carta => carta.Value).OrderDescending().ToArray();
        var grupos = valores
            .GroupBy(valor => valor)
            .Select(grupo => new { Valor = grupo.Key, Cantidad = grupo.Count() })
            .OrderByDescending(grupo => grupo.Cantidad)
            .ThenByDescending(grupo => grupo.Valor)
            .ToList();

        var mismoPalo = cartas.Select(carta => carta.Suit).Distinct().Count() == 1;
        var escaleraAlta = ObtenerCartaAltaEscalera(valores);

        if (mismoPalo && escaleraAlta > 0)
        {
            return new HandValue(8, new[] { escaleraAlta });
        }

        if (grupos[0].Cantidad == 4)
        {
            return new HandValue(7, new[] { grupos[0].Valor, grupos[1].Valor });
        }

        if (grupos[0].Cantidad == 3 && grupos[1].Cantidad == 2)
        {
            return new HandValue(6, new[] { grupos[0].Valor, grupos[1].Valor });
        }

        if (mismoPalo)
        {
            return new HandValue(5, valores);
        }

        if (escaleraAlta > 0)
        {
            return new HandValue(4, new[] { escaleraAlta });
        }

        if (grupos[0].Cantidad == 3)
        {
            return new HandValue(3, grupos.SelectMany(grupo => Enumerable.Repeat(grupo.Valor, grupo.Cantidad)).ToArray());
        }

        if (grupos[0].Cantidad == 2 && grupos[1].Cantidad == 2)
        {
            return new HandValue(2, grupos.SelectMany(grupo => Enumerable.Repeat(grupo.Valor, grupo.Cantidad)).ToArray());
        }

        if (grupos[0].Cantidad == 2)
        {
            return new HandValue(1, grupos.SelectMany(grupo => Enumerable.Repeat(grupo.Valor, grupo.Cantidad)).ToArray());
        }

        return new HandValue(0, valores);
    }

    private static int ObtenerCartaAltaEscalera(IEnumerable<int> valores)
    {
        var unicos = valores.Distinct().OrderDescending().ToList();
        if (unicos.Contains(14))
        {
            unicos.Add(1);
        }

        for (var i = 0; i <= unicos.Count - 5; i++)
        {
            if (unicos[i] - 1 == unicos[i + 1]
                && unicos[i + 1] - 1 == unicos[i + 2]
                && unicos[i + 2] - 1 == unicos[i + 3]
                && unicos[i + 3] - 1 == unicos[i + 4])
            {
                return unicos[i];
            }
        }

        return 0;
    }

    private static string Describir(int categoria)
    {
        return categoria switch
        {
            8 => "escalera de color",
            7 => "poker",
            6 => "full house",
            5 => "color",
            4 => "escalera",
            3 => "trio",
            2 => "doble pareja",
            1 => "pareja",
            _ => "carta alta"
        };
    }

    private readonly record struct Card(int Value, Suit Suit);

    private enum Suit
    {
        Hearts,
        Diamonds,
        Spades,
        Clubs
    }

    private readonly record struct HandValue(int Category, int[] Kickers) : IComparable<HandValue>
    {
        public int CompareTo(HandValue other)
        {
            var categoria = Category.CompareTo(other.Category);
            if (categoria != 0)
            {
                return categoria;
            }

            for (var i = 0; i < Math.Max(Kickers?.Length ?? 0, other.Kickers?.Length ?? 0); i++)
            {
                var actual = i < (Kickers?.Length ?? 0) ? Kickers![i] : 0;
                var rival = i < (other.Kickers?.Length ?? 0) ? other.Kickers![i] : 0;
                var comparacion = actual.CompareTo(rival);
                if (comparacion != 0)
                {
                    return comparacion;
                }
            }

            return 0;
        }
    }
}

internal sealed record OddsResult(double ProbabilidadGanar, double ProbabilidadEmpatar, string DescripcionMano);
