#!/usr/bin/env python3
"""Plot the Conditional Flow Matching training loss curve as a PNG.

Reads loss-log.csv produced by CfmTrainingLoop (columns: step, loss) and
writes an annotated line chart highlighting the initial and final loss
values. Intended for inclusion in TCC progress reports and for sharing
training results with collaborators.

Usage:
    python plot-loss-curve.py <loss-log.csv> <output.png>

Example:
    python plot-loss-curve.py loss-log.csv loss-curve.png
"""
import sys

try:
    import pandas as pd
except ImportError:
    print("pandas is required. Install with: pip install pandas")
    sys.exit(1)

try:
    import matplotlib.pyplot as plt
except ImportError:
    print("matplotlib is required. Install with: pip install matplotlib")
    sys.exit(1)


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)

    csv_path = sys.argv[1]
    out_path = sys.argv[2]

    df = pd.read_csv(csv_path)
    if 'step' not in df.columns or 'loss' not in df.columns:
        print(f"Expected columns 'step' and 'loss' in {csv_path}")
        sys.exit(1)

    fig, ax = plt.subplots(figsize=(9, 5))
    ax.plot(df['step'], df['loss'], linewidth=1.2, color='#1f77b4', label='CFM loss')

    # Highlight initial and final loss as annotated markers.
    initial_step = df['step'].iloc[0]
    initial_loss = df['loss'].iloc[0]
    final_step = df['step'].iloc[-1]
    final_loss = df['loss'].iloc[-1]
    ax.scatter([initial_step, final_step], [initial_loss, final_loss],
               color='#d62728', zorder=5)
    ax.annotate(f'{initial_loss:.3f}',
                xy=(initial_step, initial_loss),
                xytext=(20, 5), textcoords='offset points', fontsize=9)
    ax.annotate(f'{final_loss:.3f}',
                xy=(final_step, final_loss),
                xytext=(-40, 10), textcoords='offset points', fontsize=9)

    ax.set_xlabel('Training step')
    ax.set_ylabel('CFM loss')
    ax.set_title('Flow Matching Training - Iteration 1 Baseline')
    ax.grid(True, alpha=0.3)
    ax.legend(loc='upper right')

    fig.tight_layout()
    fig.savefig(out_path, dpi=140, bbox_inches='tight')
    print(f"Saved loss curve to {out_path}")


if __name__ == '__main__':
    main()
