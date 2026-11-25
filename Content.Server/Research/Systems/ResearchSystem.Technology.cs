using Content.Shared.Database;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    /// <summary>
    /// Syncs the primary entity's database to that of the secondary entity's database.
    /// </summary>
    public void Sync(EntityUid primaryUid, EntityUid otherUid, TechnologyDatabaseComponent? primaryDb = null, TechnologyDatabaseComponent? otherDb = null)
    {
        if (!Resolve(primaryUid, ref primaryDb) || !Resolve(otherUid, ref otherDb))
            return;

        primaryDb.MainDiscipline = otherDb.MainDiscipline;
        // Sunrise added start - Синхронизация расширяемых дисциплин.
        primaryDb.AdditionalDisciplines = new(otherDb.AdditionalDisciplines);
        primaryDb.ResearchDisciplineLimit = otherDb.ResearchDisciplineLimit;
        primaryDb.MaximumTechnologyTier = otherDb.MaximumTechnologyTier;
        primaryDb.StolenTechnologies = new(otherDb.StolenTechnologies);
        primaryDb.DiskRestoredRecipes = new(otherDb.DiskRestoredRecipes);
        // Sunrise added end
        primaryDb.CurrentTechnologyCards = otherDb.CurrentTechnologyCards;
        primaryDb.SupportedDisciplines = otherDb.SupportedDisciplines;
        primaryDb.UnlockedTechnologies = otherDb.UnlockedTechnologies;
        primaryDb.UnlockedRecipes = otherDb.UnlockedRecipes;

        Dirty(primaryUid, primaryDb);

        var ev = new TechnologyDatabaseSynchronizedEvent();
        RaiseLocalEvent(primaryUid, ref ev);
    }

    /// <summary>
    ///     If there's a research client component attached to the owner entity,
    ///     and the research client is connected to a research server, this method
    ///     syncs against the research server, and the server against the local database.
    /// </summary>
    /// <returns>Whether it could sync or not</returns>
    public void SyncClientWithServer(EntityUid uid, TechnologyDatabaseComponent? databaseComponent = null, ResearchClientComponent? clientComponent = null)
    {
        if (!Resolve(uid, ref databaseComponent, ref clientComponent, false))
            return;

        if (!TryComp<TechnologyDatabaseComponent>(clientComponent.Server, out var serverDatabase))
            return;

        Sync(uid, clientComponent.Server.Value, databaseComponent, serverDatabase);
    }

    /// <summary>
    /// Tries to add a technology to a database, checking if it is able to
    /// </summary>
    /// <returns>If the technology was successfully added</returns>
    public bool UnlockTechnology(EntityUid client,
        string prototypeid,
        EntityUid user,
        ResearchClientComponent? component = null,
        TechnologyDatabaseComponent? clientDatabase = null)
    {
        if (!ProtoMan.TryIndex<TechnologyPrototype>(prototypeid, out var prototype))
            return false;

        return UnlockTechnology(client, prototype, user, component, clientDatabase);
    }

    /// <summary>
    /// Tries to add a technology to a database, checking if it is able to
    /// </summary>
    /// <returns>If the technology was successfully added</returns>
    public bool UnlockTechnology(EntityUid client,
        TechnologyPrototype prototype,
        EntityUid user,
        ResearchClientComponent? component = null,
        TechnologyDatabaseComponent? clientDatabase = null)
    {
        if (!Resolve(client, ref component, ref clientDatabase, false))
            return false;

        if (!TryGetClientServer(client, out var serverEnt, out _, component))
            return false;

        if (!CanServerUnlockTechnology(client, prototype, clientDatabase, component))
            return false;

        AddTechnology(serverEnt.Value, prototype);
        ModifyServerPoints(serverEnt.Value, -prototype.Cost);
        UpdateTechnologyCards(serverEnt.Value);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} unlocked {prototype.ID} (discipline: {prototype.Discipline}, tier: {prototype.Tier}) at {ToPrettyString(client)}, for server {ToPrettyString(serverEnt.Value)}.");
        return true;
    }

    /// <summary>
    ///     Adds a technology to the database without checking if it could be unlocked.
    /// </summary>
    [PublicAPI]
    public void AddTechnology(EntityUid uid, string technology, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!ProtoMan.TryIndex<TechnologyPrototype>(technology, out var prototype))
            return;
        AddTechnology(uid, prototype, component);
    }

    /// <summary>
    ///     Adds a technology to the database without checking if it could be unlocked.
    /// </summary>
    public void AddTechnology(EntityUid uid, TechnologyPrototype technology, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Sunrise-Edit - Ограниченная база не принимает целые технологии выше своего уровня.
        if (!IsTechnologyWithinTierLimit(component, technology))
            return;

        TrySetMainDiscipline(technology, uid, component); // Sunrise-Edit - Выбор дисциплины выполняется для всех способов добавления технологии.
        foreach (var generic in technology.GenericUnlocks)
        {
            if (generic.PurchaseEvent != null)
                RaiseLocalEvent(uid, generic.PurchaseEvent); // Sunrise-Edit - Эффект применяется к конкретной базе исследований.
        }

        // Sunrise-Edit - Повторяемые технологии не сохраняются как разблокированные.
        if (!technology.Repeatable)
            component.UnlockedTechnologies.Add(technology.ID);
        var addedRecipes = new List<string>();
        foreach (var unlock in technology.RecipeUnlocks)
        {
            if (component.UnlockedRecipes.Contains(unlock))
                continue;
            component.UnlockedRecipes.Add(unlock);
            addedRecipes.Add(unlock);
        }
        Dirty(uid, component);

        var ev = new TechnologyDatabaseModifiedEvent(addedRecipes);
        RaiseLocalEvent(uid, ref ev);
    }

    /// <summary>
    ///     Returns whether a technology can be unlocked on this database,
    ///     taking parent technologies into account.
    /// </summary>
    /// <returns>Whether it could be unlocked or not</returns>
    public bool CanServerUnlockTechnology(EntityUid uid,
        TechnologyPrototype technology,
        TechnologyDatabaseComponent? database = null,
        ResearchClientComponent? client = null)
    {

        if (!Resolve(uid, ref client, ref database, false))
            return false;

        // Sunrise edit start - Доступность проверяется по общей серверной базе, а не по устаревшей копии клиента.
        if (!TryGetClientServer(uid, out var server, out var serverComp, client) ||
            !TryComp<TechnologyDatabaseComponent>(server, out var serverDatabase))
            return false;

        if (!IsTechnologyAvailable(serverDatabase, technology))
            return false;
        // Sunrise edit end

        if (technology.Cost > serverComp.Points)
            return false;

        return true;
    }

    private void OnDatabaseRegistrationChanged(EntityUid uid, TechnologyDatabaseComponent component, ref ResearchRegistrationChangedEvent args)
    {
        if (args.Server != null)
            return;
        component.MainDiscipline = null;
        component.AdditionalDisciplines.Clear(); // Sunrise-Edit - Сброс дополнительных дисциплин.
        component.ResearchDisciplineLimit = 1; // Sunrise-Edit - Восстановление исходного лимита дисциплин.
        // Sunrise added start - Сброс локальной копии состояния кражи и технологических дисков.
        component.StolenTechnologies.Clear();
        component.DiskRestoredRecipes.Clear();
        // Sunrise added end
        component.CurrentTechnologyCards = new List<string>();
        component.SupportedDisciplines = new List<ProtoId<TechDisciplinePrototype>>();
        component.UnlockedTechnologies = new List<ProtoId<TechnologyPrototype>>();
        component.UnlockedRecipes = new List<ProtoId<LatheRecipePrototype>>();
        Dirty(uid, component);
    }
}
