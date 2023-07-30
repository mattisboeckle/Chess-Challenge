using ChessChallenge.API;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Data;

public class MyBot : IChessBot
{
    static readonly int[] PIECE_VALUES = { 100, 320, 350, 500, 900, 20000 };
    static readonly int[] PIECE_PHASE = { 0, 1, 1, 2, 4 };
    static readonly ulong[] PIECE_TABLE = {0, 17876852006827220035, 17442764802556560892, 17297209133870877174, 17223739749638733806, 17876759457677835758, 17373217165325565928, 0,
                                            13255991644549399438, 17583506568768513230, 2175898572549597664, 1084293395314969850, 18090411128601117687, 17658908863988562672, 17579252489121225964, 17362482624594506424,
                                            18088114097928799212, 16144322839035775982, 18381760841018841589, 18376121450291332093, 218152002130610684, 507800692313426432, 78546933140621827, 17502669270662184681,
                                            2095587983952846102, 2166845185183979026, 804489620259737085, 17508614433633859824, 17295224476492426983, 16860632592644698081, 14986863555502077410, 17214733645651245043,
                                            2241981346783428845, 2671522937214723568, 2819295234159408375, 143848006581874414, 18303471111439576826, 218989722313687542, 143563254730914792, 16063196335921886463,
                                            649056947958124756, 17070610696300068628, 17370107729330376954, 16714810863637820148, 15990561411808821214, 17219209584983537398, 362247178929505537, 725340149412010486,
                                            0, 9255278100611888762, 4123085205260616768, 868073221978132502, 18375526489308136969, 18158510399056250115, 18086737617269097737, 0,
                                            13607044546246993624, 15920488544069483503, 16497805833213047536, 17583469180908143348, 17582910611854720244, 17434276413707386608, 16352837428273869539, 15338966700937764332,
                                            17362778423591236342, 17797653976892964347, 216178279655209729, 72628283623606014, 18085900871841415932, 17796820590280441592, 17219225120384218358, 17653536572713270000,
                                            217588987618658057, 145525853039167752, 18374121343630509317, 143834816923107843, 17941211704168088322, 17725034519661969661, 18372710631523548412, 17439054852385800698,
                                            1010791012631515130, 5929838478495476, 436031265213646066, 1812447229878734594, 1160546708477514740, 218156326927920885, 16926762663678832881, 16497506761183456745,
                                            17582909434562406605, 580992990974708984, 656996740801498119, 149207104036540411, 17871989841031265780, 18015818047948390131, 17653269455998023918, 16424899342964550108,
                                            };

    private int GetSquareBonus(int type, bool isWhite, int file, int rank)
    {
        if (isWhite)
            rank = 7 - rank;
        return (int)Math.Round(unchecked((sbyte)((PIECE_TABLE[(type * 8) + rank] >> file * 8) & 0xFF)) * 1.461);
    }

    Board board;
    Timer timer;

    Move bestMove;

    bool debug = false;
    public Move Think(Board _board, Timer _timer)
    {
        board = _board;
        timer = _timer;

        bestMove = Move.NullMove;
        int max = -10000;
        
        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            int score = -Search(-100000, 100000, 3);
            Evaluate(debug);
            if (debug) Console.WriteLine($"Score: {score}, Max: {max}");
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

        if (depth < -3) return Evaluate();

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

    int Evaluate(bool debug = false)
    {
        if (board.IsDraw()) return 0;
        if (board.IsInCheckmate()) return 100000 * SideScaleFactor;

        int eval = 0;

        // Raw piece values
        PieceList[] pieces = board.GetAllPieceLists();
        for (int i = 0; i < pieces.Length / 2; i++)
            eval += PIECE_VALUES[i] * (pieces[i].Count - pieces[i + 6].Count);

        // Mobility
        int mobility = 0;
        if (board.TrySkipTurn())
        {
            mobility -= board.GetLegalMoves().Length;
            board.UndoSkipTurn();
            mobility += board.GetLegalMoves().Length;
        }
        mobility *= 10;

        // Phase calculation
        int phase = 24;
        int CountPiecesOnBitboard(int pieceIndex, bool white)
        {
            byte count = 0;
            ulong val = board.GetPieceBitboard((PieceType)pieceIndex, white);
            while (val != 0)
            {
                if ((val & 0x1) == 0x1) count++;
                val >>= 1;
            }
            return count;
        }

        for (int i = 1; i < 6; i++)
        {
            phase -= CountPiecesOnBitboard(i, true) * PIECE_PHASE[i - 1];
            phase -= CountPiecesOnBitboard(i, false) * PIECE_PHASE[i - 1];
        }

        phase = (phase * 256 + 12) / 24;

        // PeSTO Evaluation
        int GetPieceBonus(Piece piece, bool white, bool middlegame) => GetSquareBonus((int)piece.PieceType - 1 + (middlegame ? 0 : 6), white, piece.Square.File, piece.Square.Rank);
        int middlegame = 0, endgame = 0;
        foreach (PieceList list in pieces)
        {
            foreach (Piece piece in list)
            {
                middlegame += GetPieceBonus(piece, true, true) - GetPieceBonus(piece, false, true);
                endgame += GetPieceBonus(piece, true, false) - GetPieceBonus(piece, false, false);
            }
        }

        int pestoEval = ((middlegame * (256 - phase)) + (endgame * phase)) / 256;
        if (debug)
        {
            Console.WriteLine($"PeSTO: {pestoEval}, Middle game: {middlegame}, end: {endgame}");
            Console.WriteLine($"Mobility: {mobility}, Eval: {eval}");
            Console.WriteLine($"Total: {SideScaleFactor * (mobility + pestoEval + eval)}");
        }

        return SideScaleFactor * (mobility + pestoEval + eval);
    }

    int SideScaleFactor => (board.IsWhiteToMove ? 1 : -1);
}