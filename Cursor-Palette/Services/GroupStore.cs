using System.Text.Json;
using CursorPalette.Models;

namespace CursorPalette.Services;

public static class GroupStore
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	public static List<PresetGroup> LoadAll()
	{
		if (!File.Exists(AppPaths.GroupsFile))
			return new();

		try
		{
			return JsonSerializer.Deserialize<List<PresetGroup>>(File.ReadAllText(AppPaths.GroupsFile)) ?? new();
		}
		catch
		{
			return new();
		}
	}

	private static void SaveAll(List<PresetGroup> groups) =>
		File.WriteAllText(AppPaths.GroupsFile, JsonSerializer.Serialize(groups, JsonOptions));

	public static PresetGroup Save(PresetGroup group)
	{
		var groups = LoadAll();
		var index = groups.FindIndex(candidate => candidate.Id == group.Id);

		if (index >= 0)
			groups[index] = group;
		else
			groups.Add(group);

		SaveAll(groups);

		return group;
	}

	public static void SetCollapsed(string groupId, bool collapsed)
	{
		var groups = LoadAll();
		var group = groups.FirstOrDefault(candidate => candidate.Id == groupId);

		if (group == null)
			return;

		group.Collapsed = collapsed;
		SaveAll(groups);
	}

	public static void Rename(string groupId, string name)
	{
		var groups = LoadAll();
		var group = groups.FirstOrDefault(candidate => candidate.Id == groupId);

		if (group == null)
			return;

		group.Name = name;
		SaveAll(groups);
	}

	public static void AddMember(string groupId, string presetId)
	{
		var groups = LoadAll();
		var group = groups.FirstOrDefault(candidate => candidate.Id == groupId);

		if (group == null)
			return;

		if (!group.MemberPresetIds.Contains(presetId))
			group.MemberPresetIds.Add(presetId);

		SaveAll(groups);
	}

	public static void RemoveMember(string groupId, string presetId)
	{
		var groups = LoadAll();
		var group = groups.FirstOrDefault(candidate => candidate.Id == groupId);

		if (group == null)
			return;

		group.MemberPresetIds.Remove(presetId);

		SaveAll(groups);
	}

	public static void Delete(string groupId)
	{
		var groups = LoadAll();
		groups.RemoveAll(candidate => candidate.Id == groupId);
		SaveAll(groups);
	}
}
