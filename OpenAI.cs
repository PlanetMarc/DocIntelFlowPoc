namespace PlanetTech.AI.DocIntelFlowPoc
{   

    public enum PromptRole
    {
        /// <summary>
        /// Gets the document classification, such as a deed, mining deed, divorce decree, etc.
        /// </summary>
        ClassifyDocument,

        /// <summary>
        /// Extracts key value pairs from the document. Useful for deeds where grantors and grantees are identified.
        /// </summary>
        ExtractKeyValuePairs,

        CreateFormalDocument
    }

    /// <summary>
    /// Specifies the role of the chat message.
    /// </summary>
    public class ChatMessage
    {
        public required string Role { get; set; }

        public required string Document { get; set; }
        public PromptRole PromptRole { get; set; }
        public string Content => PromptRole switch {
            PromptRole.ClassifyDocument => "Please identity the type of {document}. Please provide a simple term as your answer, such as \"Property Deed\" or \"Divorce Decree\" or \"Mining Deed\" {document}: " + Document + "",
            PromptRole.ExtractKeyValuePairs => "Please extract key value pairs of the {document} in the following format: \"Key: Value\". For example, \"Grantor: John Doe\" or \"Grantee:John Doe\" or \"Property Description: Lot 1, Block 2, Subdivision 3.\" Please output the key value pairs in a structured format. Date formats should be MM/DD/YYYY. {document}" + Document + "",
            PromptRole.CreateFormalDocument => "Please create a formal document based on the following information: \"Grantor: John Doe\" and \"Grantee: Jane Doe\" and \"Property Description: Lot 1, Block 2, Subdivision 3\". {document}:" + Document + "",
            _ => string.Empty,
        };
    }

    public class LlmResponse
    {
        public required List<Choice> Choices { get; set; }

        public class Choice
        {
            public required Message Message { get; set; }
        }

        public class Message
        {
            public required string Content { get; set; }
        }
    }
}