namespace Muonroi.RuleEngine.Abstractions
{
    /// <summary>
    /// Specifies the type of a rule in the <see cref="RuleEngine{T}"/>.
    /// </summary>
    public enum RuleType
    {
        /// <summary>
        /// Rules that validate input and should not modify state.
        /// </summary>
        Validation,

        /// <summary>
        /// Rules that encapsulate business logic and may modify state.
        /// </summary>
        Business,

        /// <summary>
        /// Rules that are used for internal purposes, such as logging or monitoring.
        /// </summary>

        EmptyTypes
    }
}
