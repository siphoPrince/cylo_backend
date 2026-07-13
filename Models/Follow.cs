namespace Cylo_Backend.Models
{
    public class Follow
    {
        public int Id { get; set; }

        // The person who clicks "Follow"
        public int FollowerId { get; set; }
        public User? Follower { get; set; }

        // The person being followed
        public int FollowingId { get; set; }
        public User? Following { get; set; }

        // Optional: track when they started following
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
