class CreateTeams < ActiveRecord::Migration
    def change
        create_table :teams do |t|
            t.string :name
            t.string :abbreviation,     index: true
            t.string :city
            t.string :state
            t.string :full_name
        end
    end
end
