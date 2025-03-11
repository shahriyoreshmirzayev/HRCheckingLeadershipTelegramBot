namespace CheckingLeadershipTelegramBot.Entities
{
    public class CandidateInfoEntities
    {
        public string FamilyName { get; set; }
        public string FirstName { get; set; }
        public string FathersName { get; set; }
        public string BirthDate { get; set; }
        public string PhoneNumber { get; set; }
        public string Position { get; set; }

        public int CandidateId { get; set; }
        public List<Question> Questions { get; set; } // Savollar ro‘yxati
        public int TotalScore { get; set; } // Umumiy ball

        public CandidateInfoEntities()
        {
            Questions = new List<Question>();
            TotalScore = 0;
        }

        public class Question
        {
            public string QuestionText { get; set; }
            public int Score { get; set; } // Savolga berilgan ball
        }
    }
}
