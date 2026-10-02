using Godot;

// 마우스로 선택한 대상과 즉시/지점 시전을 같은 입력 경로로 전달한다.
public readonly record struct SkillInput(int Slot, Node3D Target = null, Vector3? Point = null);

public interface ISkillPresentation
{
    void PlaySkill(int slot);
}
