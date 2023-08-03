using ChessChallenge.API;
using System;
using System.Linq;
using System.Data;

public class MyBot : IChessBot
{                                         // Middlegame                // Endgame
    static readonly int[] PIECE_VALUES = { 82, 337, 365, 477, 1025, 0, 94, 281, 297, 512, 936, 0};
    static readonly int[] PIECE_PHASE = { 0, 1, 1, 2, 4 };
    static readonly decimal[] PIECE_TABLE = {63746705523041458768562654720m, 71818693703096985528394040064m, 75532537544690978830456252672m, 75536154932036771593352371712m, 76774085526445040292133284352m, 3110608541636285947269332480m, 936945638387574698250991104m, 75531285965747665584902616832m,
                                            77047302762000299964198997571m, 3730792265775293618620982364m, 3121489077029470166123295018m, 3747712412930601838683035969m, 3763381335243474116535455791m, 8067176012614548496052660822m, 4977175895537975520060507415m, 2475894077091727551177487608m,
                                            2458978764687427073924784380m, 3718684080556872886692423941m, 4959037324412353051075877138m, 3135972447545098299460234261m, 4371494653131335197311645996m, 9624249097030609585804826662m, 9301461106541282841985626641m, 2793818196182115168911564530m,
                                            77683174186957799541255830262m, 4660418590176711545920359433m, 4971145620211324499469864196m, 5608211711321183125202150414m, 5617883191736004891949734160m, 7150801075091790966455611144m, 5619082524459738931006868492m, 649197923531967450704711664m,
                                            75809334407291469990832437230m, 78322691297526401047122740223m, 4348529951871323093202439165m, 4990460191572192980035045640m, 5597312470813537077508379404m, 4980755617409140165251173636m, 1890741055734852330174483975m, 76772801025035254361275759599m,
                                            75502243563200070682362835182m, 78896921543467230670583692029m, 2489164206166677455700101373m, 4338830174078735659125311481m, 4960199192571758553533648130m, 3420013420025511569771334658m, 1557077491473974933188251927m, 77376040767919248347203368440m,
                                            73949978050619586491881614568m, 77043619187199676893167803647m, 1212557245150259869494540530m, 3081561358716686153294085872m, 3392217589357453836837847030m, 1219782446916489227407330320m, 78580145051212187267589731866m, 75798434925965430405537592305m,
                                            68369566912511282590874449920m, 72396532057599326246617936384m, 75186737388538008131054524416m, 77027917484951889231108827392m, 73655004947793353634062267392m, 76417372019396591550492896512m, 74568981255592060493492515584m, 70529879645288096380279255040m};

    private int[][] UnpackedPestoTables = new int[64][];

    bool init = true;

    private int GetSquareBonus(int file, int rank, int type, bool isWhite)
    {
        if (isWhite) rank = 7 - rank;
        return UnpackedPestoTables[rank * 8 + file][type];
    }

    Board board;
    Timer timer;

    Move bestMove;

    bool debug = false;
    public Move Think(Board _board, Timer _timer)
    {
        if (init)
        {
            UnpackedPestoTables = PIECE_TABLE.Select(packedTable =>
            {
                int pieceType = 0;
                return decimal.GetBits(packedTable).Take(3)
                    .SelectMany(c => BitConverter.GetBytes(c)
                        .Select((byte square) => (int)((sbyte)square * 1.461) + PIECE_VALUES[pieceType++]))
                    .ToArray();
            }).ToArray();
            init = false;
        }

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
        int GetPieceBonus(Piece piece, bool white, bool middlegame) => GetSquareBonus(piece.Square.File, piece.Square.Rank, (int)piece.PieceType - 1 + (middlegame ? 0 : 6), white);
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