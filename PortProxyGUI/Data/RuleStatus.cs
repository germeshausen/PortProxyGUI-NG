namespace PortProxyGUI.Data;

public enum RuleStatus
{
    Inactive = 0,
    Active = 1,
    Warning = 2
}

public static class RuleReconciliation
{
    public static RuleStatus GetStatus(Rule rule, IEnumerable<Rule> systemRules) =>
        systemRules.Any(systemRule => systemRule.EqualsWithKeys(rule))
            ? RuleStatus.Active
            : rule.IsInactive ? RuleStatus.Inactive : RuleStatus.Warning;
}
