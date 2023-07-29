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
        int max = -100000;
        
        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            int score = -Search(max, -max, 4);
            board.UndoMove(move);
            if (score > max)
            {
                bestMove = move;
                max = score;
            }
        }

        Console.WriteLine($"Best {bestMove}, eval: {max}");

        return bestMove;
    }

    int Search(int alpha, int beta, int depth)
    {
        bool qsearch = depth <= 0;
        Move[] moves = board.GetLegalMoves(qsearch);
        if (moves.Length == 0) return board.IsInCheck() ? -99999 : 0;

        if (qsearch)
        {
            int stand_pat = Evaluate();
            if (stand_pat >= beta)
                return beta;
            if (alpha < stand_pat)
                alpha = stand_pat;
        }
        
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int score = -Search(-beta, -alpha, depth - 1);
            board.UndoMove(move);
            if (score >= beta)
                return beta;
            if (score > alpha)
                alpha = score;
        }
        return alpha;
    }

    int Evaluate()
    {
        if (board.IsDraw()) return 0;
        if (board.IsInCheckmate()) return 100000 * SideScaleFactor;

        int eval = 0;
        PieceList[] pieces = board.GetAllPieceLists();
        for (int i = 0; i < pieces.Length / 2; i++)
            eval += PIECE_VALUES[i] * (pieces[i].Count - pieces[i + 6].Count);
        
        return SideScaleFactor * eval;
    }

    int SideScaleFactor => (board.IsWhiteToMove ? 1 : -1);
}