class AddDivisionInformationToTeam < ActiveRecord::Migration
  def change
    add_column :teams, :division_id, :integer
    add_column :teams, :wins, :integer, default: 0
    add_column :teams, :losses, :integer, default: 0
    add_column :teams, :ot, :integer, default: 0
    add_column :teams, :so, :integer, default: 0
    add_column :teams, :points, :integer, default: 0
  end
end
