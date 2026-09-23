extends SceneTree

# Worker.tscn의 AnimationPlayer에서 직접 편집할 수 있는 3개 클립을 생성합니다.
func _initialize() -> void:
	var library := AnimationLibrary.new()
	var idle := clip(2.4, true)
	track(idle, "Visual/Torso:position", [0.0, 1.2, 2.4], [Vector3(0, .8, 0), Vector3(0, .816, 0), Vector3(0, .8, 0)])
	track(idle, "Visual/Torso:rotation", [0.0], [Vector3.ZERO])
	track(idle, "Visual/Torso/LeftArm:rotation", [0.0, 1.2, 2.4], [Vector3(0, 0, -.06), Vector3(.025, 0, -.075), Vector3(0, 0, -.06)])
	track(idle, "Visual/Torso/RightArm:rotation", [0.0], [Vector3(.05, 0, .04)])
	track(idle, "Visual/Torso/RightArm/Axe:rotation", [0.0], [Vector3(-.15, PI / 2, -.15)])
	library.add_animation("Idle", idle)

	# 준비 → 내려치기 → 복귀. 다리와 유닛 루트 위치는 움직이지 않습니다.
	var swing := clip(.8, false)
	var times := [0.0, .3, .43, .53, .8]
	track(swing, "Visual/Torso:position", times, [Vector3(0, .8, 0), Vector3(0, .825, 0), Vector3(0, .785, 0), Vector3(0, .79, 0), Vector3(0, .8, 0)])
	track(swing, "Visual/Torso:rotation", times, [Vector3.ZERO, Vector3(.10, -.12, 0), Vector3(-.20, .10, 0), Vector3(-.17, .10, 0), Vector3.ZERO])
	track(swing, "Visual/Torso/RightArm:rotation", times, [Vector3(.05, 0, .04), Vector3(2.55, -.12, -.15), Vector3(1.1, 0, -.10), Vector3(1.05, 0, -.10), Vector3(.05, 0, .04)])
	track(swing, "Visual/Torso/LeftArm:rotation", times, [Vector3(0, 0, -.06), Vector3(.55, 0, -.28), Vector3(.9, 0, -.2), Vector3(.8, 0, -.2), Vector3(0, 0, -.06)])
	track(swing, "Visual/Torso/RightArm/Axe:rotation", times, [Vector3(-.15, PI / 2, -.15), Vector3(-.6, PI / 2, 0), Vector3(-2.3, PI / 2, 0), Vector3(-2.3, PI / 2, 0), Vector3(-.15, PI / 2, -.15)])
	library.add_animation("Swing", swing)

	var carry := clip(2.0, true)
	track(carry, "Visual/Torso:position", [0.0, 1.0, 2.0], [Vector3(0, .8, 0), Vector3(0, .811, 0), Vector3(0, .8, 0)])
	track(carry, "Visual/Torso:rotation", [0.0], [Vector3(.055, 0, 0)])
	track(carry, "Visual/Torso/LeftArm:rotation", [0.0], [Vector3(1.08, 0, .14)])
	track(carry, "Visual/Torso/RightArm:rotation", [0.0], [Vector3(1.02, 0, -.14)])
	track(carry, "Visual/Torso/RightArm/Axe:rotation", [0.0], [Vector3(-.15, PI / 2, -.15)])
	library.add_animation("Carry", carry)
	DirAccess.make_dir_recursive_absolute("res://units/animations")
	var result := ResourceSaver.save(library, "res://units/animations/Worker.tres")
	print("Worker animation library: ", error_string(result))
	quit(0 if result == OK else 1)

func clip(duration: float, loop: bool) -> Animation:
	var animation := Animation.new()
	animation.length = duration
	animation.loop_mode = Animation.LOOP_LINEAR if loop else Animation.LOOP_NONE
	return animation

func track(animation: Animation, path: String, times: Array, values: Array) -> void:
	var index := animation.add_track(Animation.TYPE_VALUE)
	animation.track_set_path(index, NodePath(path))
	for i in times.size():
		animation.track_insert_key(index, times[i], values[i])
