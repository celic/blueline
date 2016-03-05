class MoveTeamFromGoalieToGoalieStats < ActiveRecord::Migration
  def change
  	add_column :goalie_stats, :team_id, :integer
  end
end
