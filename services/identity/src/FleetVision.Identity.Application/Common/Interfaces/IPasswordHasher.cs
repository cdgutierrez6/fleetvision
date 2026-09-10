namespace FleetVision.Identity.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);

    /// <summary>
    /// A pre-computed, <b>valid</b> Argon2id hash produced with the exact same
    /// cost parameters (memory, iterations, parallelism) as <see cref="Hash"/>.
    /// <para>
    /// Its sole purpose is to defend against user-enumeration timing attacks on login:
    /// when the account does not exist, the handler verifies the supplied password
    /// against <c>DummyHash</c> instead of skipping verification. Because
    /// <see cref="Verify"/> derives its cost parameters from the hash string itself,
    /// verifying against <c>DummyHash</c> runs the full Argon2id KDF at the very same
    /// cost as verifying a real user's hash. This equalises the <i>dominant</i> cost of
    /// the "user exists" and "user does not exist" branches, closing the timing oracle
    /// that would otherwise let an attacker enumerate valid emails. (A second-order
    /// residual from the indexed user lookup remains, orders of magnitude below the KDF.)
    /// </para>
    /// <para>
    /// It must be a real hash — never a malformed literal — so it is guaranteed to
    /// reach the KDF. It is derived from a random password, so <see cref="Verify"/>
    /// against it returns <c>false</c> for any input (barring astronomically unlikely
    /// collision) and it can never authenticate anyone.
    /// </para>
    /// </summary>
    string DummyHash { get; }
}
