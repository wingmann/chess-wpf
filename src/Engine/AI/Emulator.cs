﻿using Engine.Conditions;
using Engine.UtilityComponents;

namespace Engine.AI;

internal class Emulator
{
    private static readonly Random Random = new();
    private static readonly Dictionary<char, int> PiecesValues = new();

    static Emulator()
    {
        PiecesValues.Add('p', 1);
        PiecesValues.Add('x', 1);
        PiecesValues.Add('j', 3);
        PiecesValues.Add('s', 3);
        PiecesValues.Add('v', 5);
        PiecesValues.Add('d', 9);
    }

    /// <summary>
    /// If this field is set to true, the multithreading calculation is stopped asap.
    /// </summary>
    public static bool InterruptHalfTurn;

    /// <summary>
    /// Selects halfTurn from calculated condition. Calculation is done in parallel.
    /// </summary>
    public HalfTurn? BestHalfTurn(CalculatedCondition calcCondition, Condition condition, int depth)
    {
        InterruptHalfTurn = false;
        var possibleHalfTurns = PossibleHalfTurns(calcCondition);

        if (possibleHalfTurns.Count is 0)
        {
            return null;
        }

        ChessEngine.ProgressMaximumStatic = possibleHalfTurns.Count;

        Parallel.For(0, possibleHalfTurns.Count, i =>
        {
            BasicEvaluating(condition, possibleHalfTurns[i]);

            Condition newCondition = new(condition);
            ChessEngine.MovePiece(possibleHalfTurns[i].From, possibleHalfTurns[i].To, newCondition);
            CalculatedCondition newCalculatedCondition = new(newCondition);

            possibleHalfTurns[i].Value -= Minimax(newCalculatedCondition, newCondition, depth - 1);
            Interlocked.Increment(ref ChessEngine.ProgressValueStatic);
        });

        return InterruptHalfTurn ? null : BestHalfTurn(possibleHalfTurns);
    }

    /// <summary>
    /// Finding the best moves and selecting one random of them.
    /// </summary>
    private static HalfTurn BestHalfTurn(IReadOnlyList<HalfTurn> possibleMoves)
    {
        List<HalfTurn> bestMoves = new() {possibleMoves[0]};

        for (var i = 1; i < possibleMoves.Count; i++)
        {
            if (possibleMoves[i].Value > bestMoves[0].Value)
            {
                bestMoves = new List<HalfTurn> {possibleMoves[i]};
            }
            else if (possibleMoves[i].Value == bestMoves[0].Value)
            {
                bestMoves.Add(possibleMoves[i]);
            }
        }

        return bestMoves[Random.Next(bestMoves.Count)];
    }

    /// <summary>
    /// Loads all possible moves of pieces.
    /// </summary>
    private static List<HalfTurn> PossibleHalfTurns(CalculatedCondition calculatedCondition)
    {
        return (from piece in calculatedCondition.PiecesOnTurn
            from coords in piece.Value.PossibleMoves
            select new HalfTurn(piece.Key, coords)).ToList();
    }

    /// <summary>
    /// Algorithm for finding the best move.
    /// </summary>
    /// <returns>Returns value of the best move.</returns>
    private static int Minimax(CalculatedCondition calcCondition, Condition condition, int depth)
    {
        if (InterruptHalfTurn) return int.MaxValue;

        var possibleHalfTurns = PossibleHalfTurns(calcCondition);
        var max = int.MinValue;

        if (depth == 0)
        {
            foreach (HalfTurn halfTurn in possibleHalfTurns)
            {
                BasicEvaluating(condition, halfTurn);

                if (CalculatedCondition.GetDataOfCalculatedSituation(condition)!.EnemyPossibleAttacks
                    .Contains(halfTurn.To))
                {
                    halfTurn.Value -=
                        PiecesValues[condition.Chessboard[halfTurn.From.Row, halfTurn.From.Column].Status];
                }

                if (max < halfTurn.Value)
                {
                    max = halfTurn.Value;
                }
            }

            return max;
        }

        foreach (HalfTurn halfTurn in possibleHalfTurns)
        {
            BasicEvaluating(condition, halfTurn);

            Condition newCondition = new(condition);
            ChessEngine.MovePiece(halfTurn.From, halfTurn.To, newCondition);
            CalculatedCondition calculatedCondition = new(newCondition);

            if (calcCondition.DrawMate)
            {
                return calcCondition.Check ? 500 : 0;
            }

            halfTurn.Value -= Minimax(calculatedCondition, newCondition, depth - 1);

            if (halfTurn.Value > max)
            {
                max = halfTurn.Value;
            }
        }

        return max;
    }

    private static void BasicEvaluating(Condition condition, HalfTurn halfTurn)
    {
        var toStatus = condition.Chessboard[halfTurn.To.Row, halfTurn.To.Column].Status;
        var fromStatus = condition.Chessboard[halfTurn.From.Row, halfTurn.From.Column].Status;

        // Evaluating taking enemy piece.
        if (toStatus is not 'n')
        {
            // Evaluating en passant (only pawn can take pawn en passant).
            if (toStatus is not 'x' && fromStatus is 'p')
            {
                halfTurn.Value += PiecesValues[toStatus];
            }
        }

        if (fromStatus is not 'p')
        {
            // Evaluating pawn at the ends of row.
            return;
        }

        if (halfTurn.To.Row is 0 or 7)
        {
            halfTurn.Value += 4;
        }
    }
}