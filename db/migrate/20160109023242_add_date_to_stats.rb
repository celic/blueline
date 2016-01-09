class AddDateToStats < ActiveRecord::Migration
    def change
        add_column :player_stats, :date, :date
        add_column :goalie_stats, :date, :date
    end
end
