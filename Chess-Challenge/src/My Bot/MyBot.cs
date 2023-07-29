using ChessChallenge.API;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Data;

public class MyBot : IChessBot
{
    static readonly int[] PIECE_VALUES = { 100, 320, 350, 500, 900, 20000 };

    Board board;
    Timer timer;

    Move bestMove;
    public Move Think(Board _board, Timer _timer)
    {
        board = _board;
        timer = _timer;

        bestMove = Move.NullMove;
        int max = -99999;

        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            int score = -Search(3);
            board.UndoMove(move);

            if (score > max)
            {
                bestMove = move;
                max = score;
            }
        }

        return bestMove;
    }

    int Search(int depth)
    {
        if (depth == 0) return Evaluate();

        int max = -99999;
        foreach (Move move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            int score = -Search(depth - 1);
            board.UndoMove(move);
            if (score > max)
            {
                max = score;
            }
        }
        return max;
    }

    int Evaluate()
    {
        int sideScaleFactor = (board.IsWhiteToMove ? 1 : -1);
        if (board.IsDraw()) return 0;
        if (board.IsInCheckmate()) return 100000 * sideScaleFactor;

        int eval = 0;
        PieceList[] pieces = board.GetAllPieceLists();
        for (int i = 0; i < pieces.Length / 2; i++)
        {
            eval += PIECE_VALUES[i] * (pieces[i].Count - pieces[i + 6].Count);
        }
        
        return sideScaleFactor * eval;
    }
}