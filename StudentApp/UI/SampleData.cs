namespace StudentApp.UI;

/// <summary>Demo records so semantic search can be shown immediately (menu option 9).</summary>
internal static class SampleData
{
    public static IReadOnlyList<(int Id, string Name, int Age, string Profile)> Students { get; } =
    [
        (1001, "Aarav Sharma", 21, "Passionate about software development, Python coding and building web applications"),
        (1002, "Meera Iyer", 22, "Cybersecurity enthusiast: ethical hacking, network defense and protecting systems"),
        (1003, "Rohan Mehta", 20, "Plays football, loves fitness and team leadership; captain of the college team"),
        (1004, "Sana Khan", 23, "Machine learning and data science, neural networks, statistics and research"),
        (1005, "Diya Patel", 19, "UI/UX design, illustration and creative graphics; learning Figma and frontend work"),
        (1006, "Kabir Singh", 24, "Cloud infrastructure, AWS, Docker and DevOps automation"),
        (1007, "Isha Nair", 22, "Writing, literature and poetry; wants to become a journalist"),
        (1008, "Vikram Rao", 25, "Entrepreneurship, startups, marketing and finance"),
        (1009, "Anika Das", 21, "Singing, guitar and music production")
    ];
}
