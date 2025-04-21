using MessagePack;

namespace LuxRunner
{

    static class TileType
    {
        public const int Unknown = -1;
        public const int Empty = 0;
        public const int Nebula = 1;
        public const int Asteroid = 2;
    }

    public class LuxActions
    {
        public int[][] action { get; set; }
    }

    [MessagePackObject]
    public class LuxState
    {
        [Key(0)]
        public Obs obs { get; set; }

        [Key(1)]
        public int step { get; set; }

        [Key(2)]
        public double remainingOverageTime { get; set; }

        [Key(3)]
        public string player { get; set; }

        [Key(4)]
        public float reward { get; set; }

        [Key(5)]
        public Info info { get; set; }
    }

    [MessagePackObject]
    public class Obs
    {
        [Key(0)]
        public Units units { get; set; }

        [Key(1)]
        public bool[][] units_mask { get; set; }

        [Key(2)]
        public bool[][] sensor_mask { get; set; }

        [Key(3)]
        public Map_Features map_features { get; set; }

        [Key(4)]
        public int[][] relic_nodes { get; set; }

        [Key(5)]
        public bool[] relic_nodes_mask { get; set; }

        [Key(6)]
        public int[] team_points { get; set; }

        [Key(7)]
        public int[] team_wins { get; set; }

        [Key(8)]
        public int steps { get; set; }

        [Key(9)]
        public int match_steps { get; set; }
    }

    [MessagePackObject]
    public class Units
    {
        [Key(0)]
        public int[][][] position { get; set; }

        [Key(1)]
        public int[][] energy { get; set; }
    }

    [MessagePackObject]
    public class Map_Features
    {
        [Key(0)]
        public int[][] energy { get; set; }

        [Key(1)]
        public int[][] tile_type { get; set; }
    }

    [MessagePackObject]
    public class Info
    {
        [Key(0)]
        public Env_Cfg env_cfg { get; set; }
    }

    [MessagePackObject]
    public class Env_Cfg
    {
        [Key(0)]
        public int max_units { get; set; }

        [Key(1)]
        public int match_count_per_episode { get; set; }

        [Key(2)]
        public int max_steps_in_match { get; set; }

        [Key(3)]
        public int map_height { get; set; }

        [Key(4)]
        public int map_width { get; set; }

        [Key(5)]
        public int num_teams { get; set; }

        [Key(6)]
        public int unit_move_cost { get; set; }

        [Key(7)]
        public int unit_sap_cost { get; set; }

        [Key(8)]
        public int unit_sap_range { get; set; }

        [Key(9)]
        public int unit_sensor_range { get; set; }
    }

}