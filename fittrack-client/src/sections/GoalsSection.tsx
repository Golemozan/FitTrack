import { useEffect, useState } from "react";
import { Target } from "lucide-react";
import { useToast } from "../hooks/useToast";
import { useGoals, useUpdateGoals } from "../hooks/useGoals";
import { useProfile, useUpdateProfile } from "../hooks/useCoach";
import { useWeightStats } from "../hooks/useWeight";
import { Button, Card, Input, ListSkeleton } from "../components/ui";
import { SectionHeader } from "../components/SectionHeader";

const FIELDS = [
  { key: "calorieGoal", label: "Kalori (kcal)" },
  { key: "proteinGoal", label: "Protein (g)" },
  { key: "carbGoal", label: "Karbonhidrat (g)" },
  { key: "fatGoal", label: "Yağ (g)" },
] as const;

export default function GoalsSection() {
  const toast = useToast();
  const goals = useGoals();
  const updateGoals = useUpdateGoals();

  const [form, setForm] = useState<Record<string, string>>({});

  useEffect(() => {
    if (goals.data) {
      setForm({
        calorieGoal: String(goals.data.calorieGoal),
        proteinGoal: String(goals.data.proteinGoal),
        carbGoal: String(goals.data.carbGoal),
        fatGoal: String(goals.data.fatGoal),
      });
    }
  }, [goals.data]);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    updateGoals.mutate(
      {
        calorieGoal: Number(form.calorieGoal),
        proteinGoal: Number(form.proteinGoal),
        carbGoal: Number(form.carbGoal),
        fatGoal: Number(form.fatGoal),
      },
      {
        onSuccess: () => toast.success("Hedefler kaydedildi"),
        onError: () => toast.error("Hedefler kaydedilemedi"),
      }
    );
  };

  return (
    <section id="hedefler" className="scroll-mt-24">
      <SectionHeader icon={Target} title="Hedefler & Profil" />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <h3 className="mb-3 text-sm font-semibold text-neutral-300">Günlük makro hedefleri</h3>
          {goals.isLoading ? (
            <ListSkeleton rows={2} />
          ) : (
            <form onSubmit={submit} className="grid grid-cols-2 gap-3">
              {FIELDS.map((f) => (
                <Input
                  key={f.key}
                  label={f.label}
                  type="number"
                  value={form[f.key] ?? ""}
                  onChange={(e) => setForm({ ...form, [f.key]: e.target.value })}
                />
              ))}
              <div className="col-span-2">
                <Button type="submit" disabled={updateGoals.isPending}>Kaydet</Button>
              </div>
            </form>
          )}
        </Card>

        <ProfileCard />
      </div>
    </section>
  );
}

// Height + target weight — persistent personal constants. Also surfaces BMI.
function ProfileCard() {
  const profile = useProfile();
  const updateProfile = useUpdateProfile();
  const stats = useWeightStats();
  const toast = useToast();

  const [height, setHeight] = useState("");
  const [target, setTarget] = useState("");

  useEffect(() => {
    if (profile.data) {
      setHeight(profile.data.heightCm != null ? String(profile.data.heightCm) : "");
      setTarget(profile.data.targetWeightKg != null ? String(profile.data.targetWeightKg) : "");
    }
  }, [profile.data]);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    updateProfile.mutate(
      {
        heightCm: height ? Number(height) : null,
        targetWeightKg: target ? Number(target) : null,
      },
      {
        onSuccess: () => toast.success("Profil kaydedildi"),
        onError: () => toast.error("Kaydedilemedi"),
      }
    );
  };

  const current = stats.data?.currentWeight ?? null;
  const h = Number(height);
  const bmi = h > 0 && current != null ? current / (h / 100) ** 2 : null;
  const toTarget = target && current != null ? current - Number(target) : null;

  return (
    <Card>
      <h3 className="mb-3 text-sm font-semibold text-neutral-300">Profil</h3>
      <form onSubmit={submit} className="grid grid-cols-2 gap-3">
        <Input label="Boy (cm)" type="number" value={height} onChange={(e) => setHeight(e.target.value)} />
        <Input label="Hedef Kilo (kg)" type="number" step="0.1" value={target} onChange={(e) => setTarget(e.target.value)} />
        <div className="col-span-2">
          <Button type="submit" disabled={updateProfile.isPending}>Kaydet</Button>
        </div>
      </form>

      {(bmi != null || toTarget != null) && (
        <div className="mt-4 grid grid-cols-2 gap-3 border-t border-hair pt-4">
          <div>
            <div className="text-xs font-medium text-neutral-400">BMI</div>
            <div className="num text-2xl text-neutral-100">{bmi != null ? bmi.toFixed(1) : "—"}</div>
          </div>
          <div>
            <div className="text-xs font-medium text-neutral-400">Hedefe kalan</div>
            <div className={`num text-2xl ${toTarget != null && toTarget > 0 ? "text-accent" : "text-loss"}`}>
              {toTarget != null ? `${toTarget > 0 ? "" : "+"}${Math.abs(toTarget).toFixed(1)}` : "—"}
              {toTarget != null && <span className="ml-1 text-sm text-neutral-400">kg</span>}
            </div>
          </div>
        </div>
      )}
    </Card>
  );
}
