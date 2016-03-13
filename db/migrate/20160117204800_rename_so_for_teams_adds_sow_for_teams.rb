class RenameSoForTeamsAddsSowForTeams < ActiveRecord::Migration
  def change
    rename_column :teams, :so, :sol
    add_column :teams, :sow, :integer, default: 0
  end
end
